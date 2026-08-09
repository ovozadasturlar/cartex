using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public sealed record GetCustomerStatementQuery(
    long CustomerId,
    DateTime? From = null,
    DateTime? To = null,
    long? TradeCaseId = null,
    long? BranchId = null,
    string? DocumentTypes = null) : IRequest<CustomerStatementDto>;

internal sealed record AccountRow(long Id, AccountType Type, string Currency);
internal sealed record RawStatementEntry(
    DateTime OccurredAt,
    string Type,
    long? DocumentId,
    string DocumentNumber,
    string Summary,
    decimal Delta,
    string Currency,
    long? SaleId,
    long? TradeCaseId,
    long Order);

internal sealed class ProductAccumulator(long variantId, string name, string unit)
{
    public long VariantId { get; } = variantId;
    public string Name { get; } = name;
    public string Unit { get; } = unit;
    public decimal Sold { get; set; }
    public decimal SaleReturned { get; set; }
    public decimal CustodyIssued { get; set; }
    public decimal CustodyReturned { get; set; }
    public decimal CustodySettled { get; set; }
    public decimal Charged { get; set; }
}

public sealed class GetCustomerStatementQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetCustomerStatementQuery, CustomerStatementDto>
{
    public async Task<CustomerStatementDto> Handle(
        GetCustomerStatementQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Statements.View))
            throw new ForbiddenException("Hisob ko'chirmasini ko'rishga ruxsat yo'q.");
        if (request.To.HasValue && request.From.HasValue && request.To <= request.From)
            throw new BusinessRuleException("Davr sanalari noto'g'ri.", "invalid_date_range");
        if (request.BranchId is { } branchId && !currentUser.CanAccessAllBranches
            && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Bu filialga ruxsat yo'q.");

        var customerQuery = db.Customers.AsNoTracking().Where(x => x.Id == request.CustomerId);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customerQuery = customerQuery.Where(x => x.AssignedUserId == currentUser.UserId);
        var customer = await customerQuery
            .Select(x => new { x.Id, x.FullName, x.Phone })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");

        string? tradeCaseNumber = null;
        if (request.TradeCaseId is { } tradeCaseId)
        {
            tradeCaseNumber = await db.TradeCases.AsNoTracking()
                .Where(x => x.Id == tradeCaseId && x.CustomerId == request.CustomerId)
                .Select(x => x.CaseNumber)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");
        }

        var baseCurrency = (await db.Businesses.AsNoTracking().Select(x => x.Currency)
            .FirstAsync(cancellationToken)).ToUpperInvariant();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId
                        && (x.Type == AccountType.Debt || x.Type == AccountType.CustomerAdvance))
            .Select(x => new AccountRow(x.Id, x.Type, x.Currency))
            .ToListAsync(cancellationToken);
        var accountById = accounts.ToDictionary(x => x.Id);
        var accountIds = accountById.Keys.ToList();

        var opening = accounts.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, _ => 0m, StringComparer.OrdinalIgnoreCase);
        if (request.From.HasValue && accountIds.Count > 0)
        {
            var prior = db.Transactions.AsNoTracking()
                .Where(x => x.CreatedAt < request.From.Value
                            && ((x.FromAccountId != null && accountIds.Contains(x.FromAccountId.Value))
                                || (x.ToAccountId != null && accountIds.Contains(x.ToAccountId.Value))));
            prior = ApplyTransactionScope(prior, request);
            var incoming = await prior
                .Where(x => x.ToAccountId != null && accountIds.Contains(x.ToAccountId.Value))
                .GroupBy(x => x.ToAccountId!.Value)
                .Select(x => new { AccountId = x.Key, Amount = x.Sum(row => row.Amount) })
                .ToListAsync(cancellationToken);
            var outgoing = await prior
                .Where(x => x.FromAccountId != null && accountIds.Contains(x.FromAccountId.Value))
                .GroupBy(x => x.FromAccountId!.Value)
                .Select(x => new { AccountId = x.Key, Amount = x.Sum(row => row.Amount) })
                .ToListAsync(cancellationToken);
            foreach (var row in incoming)
                AddBalance(opening, accountById[row.AccountId], row.Amount);
            foreach (var row in outgoing)
                AddBalance(opening, accountById[row.AccountId], -row.Amount);
        }

        var raw = new List<RawStatementEntry>();
        if (accountIds.Count > 0)
        {
            var transactions = db.Transactions.AsNoTracking()
                .Where(x => (x.FromAccountId != null && accountIds.Contains(x.FromAccountId.Value))
                            || (x.ToAccountId != null && accountIds.Contains(x.ToAccountId.Value)));
            transactions = ApplyTransactionScope(transactions, request);
            if (request.From.HasValue) transactions = transactions.Where(x => x.CreatedAt >= request.From.Value);
            if (request.To.HasValue) transactions = transactions.Where(x => x.CreatedAt < request.To.Value);

            var rows = await transactions.Select(x => new
            {
                x.Id, x.CreatedAt, x.OperationType, x.Amount, x.FromAccountId, x.ToAccountId,
                x.SaleId, x.TradeCaseId,
                x.CustomerPaymentDocumentId,
                PaymentNumber = x.CustomerPaymentDocument != null ? x.CustomerPaymentDocument.DocumentNumber : null,
                x.CustomerReturnDocumentId,
                ReturnNumber = x.CustomerReturnDocument != null ? x.CustomerReturnDocument.DocumentNumber : null,
                x.CustomerRefundDocumentId,
                RefundNumber = x.CustomerRefundDocument != null ? x.CustomerRefundDocument.DocumentNumber : null,
                SaleReceipt = x.Sale != null ? x.Sale.ReceiptToken : null,
                CaseNumber = x.TradeCase != null ? x.TradeCase.CaseNumber : null,
                x.Description
            }).ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (row.FromAccountId is long fromId && accountById.TryGetValue(fromId, out var from))
                    AddTransaction(row.Id, row.CreatedAt, row.OperationType, -row.Amount, from,
                        row.CustomerPaymentDocumentId, row.PaymentNumber,
                        row.CustomerReturnDocumentId, row.ReturnNumber,
                        row.CustomerRefundDocumentId, row.RefundNumber,
                        row.SaleId, row.SaleReceipt, row.TradeCaseId, row.CaseNumber, row.Description);
                if (row.ToAccountId is long toId && accountById.TryGetValue(toId, out var to))
                    AddTransaction(row.Id, row.CreatedAt, row.OperationType, row.Amount, to,
                        row.CustomerPaymentDocumentId, row.PaymentNumber,
                        row.CustomerReturnDocumentId, row.ReturnNumber,
                        row.CustomerRefundDocumentId, row.RefundNumber,
                        row.SaleId, row.SaleReceipt, row.TradeCaseId, row.CaseNumber, row.Description);
            }

            void AddTransaction(
                long transactionId,
                DateTime occurredAt,
                OperationType operation,
                decimal accountDelta,
                AccountRow account,
                long? paymentId,
                string? paymentNumber,
                long? returnId,
                string? returnNumber,
                long? refundId,
                string? refundNumber,
                long? saleId,
                string? saleReceipt,
                long? caseId,
                string? caseNumber,
                string? description)
            {
                var delta = account.Type == AccountType.Debt ? accountDelta : -accountDelta;
                var type = paymentId.HasValue ? "CustomerPayment"
                    : returnId.HasValue ? "CustomerReturn"
                    : refundId.HasValue ? "CustomerRefund"
                    : saleId.HasValue ? "Sale"
                    : caseId.HasValue ? "TradeCase"
                    : operation.ToString();
                var documentId = paymentId ?? returnId ?? refundId ?? saleId ?? caseId ?? transactionId;
                var number = paymentNumber ?? returnNumber ?? refundNumber
                             ?? (saleId.HasValue ? $"SALE-{saleId}" : null)
                             ?? caseNumber ?? description ?? $"TX-{transactionId}";
                raw.Add(new RawStatementEntry(occurredAt, type, documentId, number,
                    Summary(operation), delta, account.Currency, saleId, caseId, transactionId));
            }
        }

        await AddOperationalRowsAsync(raw, request, cancellationToken);
        var grouped = raw
            .GroupBy(x => new { x.Type, x.DocumentId, x.DocumentNumber, x.Currency, x.SaleId, x.TradeCaseId })
            .Select(x => new RawStatementEntry(
                x.Min(row => row.OccurredAt), x.Key.Type, x.Key.DocumentId, x.Key.DocumentNumber,
                x.OrderBy(row => row.Order).Select(row => row.Summary).First(),
                x.Sum(row => row.Delta), x.Key.Currency, x.Key.SaleId, x.Key.TradeCaseId,
                x.Min(row => row.Order)))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Order).ToList();

        var running = new Dictionary<string, decimal>(opening, StringComparer.OrdinalIgnoreCase);
        var allTimeline = new List<CustomerStatementEntryDto>(grouped.Count);
        foreach (var row in grouped)
        {
            var balance = running.GetValueOrDefault(row.Currency) + row.Delta;
            running[row.Currency] = balance;
            allTimeline.Add(new CustomerStatementEntryDto(
                row.OccurredAt, row.Type, row.DocumentId, row.DocumentNumber, row.Summary,
                Math.Max(0, row.Delta), Math.Max(0, -row.Delta), balance, row.Currency,
                row.SaleId, row.TradeCaseId));
        }

        var requestedTypes = string.IsNullOrWhiteSpace(request.DocumentTypes)
            ? null
            : request.DocumentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var timeline = requestedTypes is null
            ? allTimeline
            : allTimeline.Where(x => requestedTypes.Contains(x.Type)).ToList();
        var currencies = opening.Keys.Concat(running.Keys).Append(baseCurrency)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var balances = currencies.Select(code => new CustomerStatementBalanceDto(
            code, opening.GetValueOrDefault(code), running.GetValueOrDefault(code))).ToList();
        var products = await BuildProductsAsync(request, cancellationToken);

        return new CustomerStatementDto(customer.Id, customer.FullName, customer.Phone,
            request.From, request.To, request.TradeCaseId, tradeCaseNumber, request.BranchId,
            baseCurrency, balances, timeline, products, DateTime.UtcNow);
    }

    private IQueryable<Transaction> ApplyTransactionScope(IQueryable<Transaction> query, GetCustomerStatementQuery request)
    {
        if (request.BranchId is { } branchId) query = query.Where(x => x.BranchId == branchId);
        else if (!currentUser.CanAccessAllBranches) query = query.Where(x => x.BranchId == null || currentUser.BranchIds.Contains(x.BranchId.Value));
        if (request.TradeCaseId is { } tradeCaseId) query = query.Where(x => x.TradeCaseId == tradeCaseId);
        return query;
    }

    private async Task AddOperationalRowsAsync(
        List<RawStatementEntry> raw,
        GetCustomerStatementQuery request,
        CancellationToken cancellationToken)
    {
        var knownSales = raw.Where(x => x.Type == "Sale" && x.SaleId.HasValue).Select(x => x.SaleId!.Value).ToHashSet();
        var sales = db.Sales.AsNoTracking().Where(x => x.CustomerId == request.CustomerId);
        sales = Scope(sales, x => x.BranchId, x => x.TradeCaseId, x => x.CreatedAt, request);
        var saleRows = await sales.Select(x => new
        {
            x.Id, x.CreatedAt, x.TradeCaseId, x.ReceiptToken, x.TotalAmount, x.DebtCurrency
        }).ToListAsync(cancellationToken);
        raw.AddRange(saleRows.Where(x => !knownSales.Contains(x.Id)).Select(x => new RawStatementEntry(
            x.CreatedAt, "Sale", x.Id, $"SALE-{x.Id}", $"Savdo: {x.TotalAmount:N2}",
            0, x.DebtCurrency, x.Id, x.TradeCaseId, x.Id)));

        var knownReturns = raw.Where(x => x.Type == "CustomerReturn" && x.DocumentId.HasValue)
            .Select(x => x.DocumentId!.Value).ToHashSet();
        var returns = db.CustomerReturnDocuments.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId && x.Status == BusinessDocumentStatus.Posted);
        returns = Scope(returns, x => x.BranchId, x => x.Sale.TradeCaseId, x => x.CreatedAt, request);
        var returnRows = await returns.Select(x => new
        {
            x.Id, x.DocumentNumber, x.CreatedAt, x.SaleId, x.Sale.TradeCaseId, x.RefundAmount,
            Currency = x.Sale.DebtCurrency
        }).ToListAsync(cancellationToken);
        raw.AddRange(returnRows.Where(x => !knownReturns.Contains(x.Id)).Select(x => new RawStatementEntry(
            x.CreatedAt, "CustomerReturn", x.Id, x.DocumentNumber, $"Savdo qaytarildi: {x.RefundAmount:N2}",
            0, x.Currency, x.SaleId, x.TradeCaseId, x.Id)));

        var issues = db.GoodsIssueDocuments.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId && x.Status == BusinessDocumentStatus.Posted);
        issues = Scope(issues, x => x.BranchId, x => (long?)x.TradeCaseId, x => x.CreatedAt, request);
        var issueRows = await issues.Select(x => new
        {
            x.Id, x.DocumentNumber, x.CreatedAt, x.TradeCaseId, x.Currency,
            Quantity = x.Lines.Sum(line => line.Quantity)
        }).ToListAsync(cancellationToken);
        raw.AddRange(issueRows.Select(x => new RawStatementEntry(x.CreatedAt, "GoodsIssue", x.Id,
            x.DocumentNumber, $"Mahsulot berildi: {x.Quantity:0.###}", 0, x.Currency,
            null, x.TradeCaseId, x.Id)));

        var goodsReturns = db.GoodsReturnDocuments.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId && x.Status == BusinessDocumentStatus.Posted);
        goodsReturns = Scope(goodsReturns, x => x.BranchId, x => (long?)x.TradeCaseId, x => x.CreatedAt, request);
        var goodsReturnRows = await goodsReturns.Select(x => new
        {
            x.Id, x.DocumentNumber, x.CreatedAt, x.TradeCaseId,
            Currency = x.TradeCase.Currency, Quantity = x.Lines.Sum(line => line.Quantity)
        }).ToListAsync(cancellationToken);
        raw.AddRange(goodsReturnRows.Select(x => new RawStatementEntry(x.CreatedAt, "GoodsReturn", x.Id,
            x.DocumentNumber, $"Mahsulot qaytdi: {x.Quantity:0.###}", 0, x.Currency,
            null, x.TradeCaseId, x.Id)));
    }

    private async Task<List<CustomerStatementProductDto>> BuildProductsAsync(
        GetCustomerStatementQuery request,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, ProductAccumulator>();
        ProductAccumulator Row(long id, string name, string unit)
        {
            if (!map.TryGetValue(id, out var value))
                map[id] = value = new ProductAccumulator(id, name, unit);
            return value;
        }

        var saleLines = db.SaleItems.AsNoTracking().Where(x => x.Sale.CustomerId == request.CustomerId);
        if (request.BranchId is { } branchId) saleLines = saleLines.Where(x => x.Sale.BranchId == branchId);
        else if (!currentUser.CanAccessAllBranches) saleLines = saleLines.Where(x => currentUser.BranchIds.Contains(x.Sale.BranchId));
        if (request.TradeCaseId is { } tradeCaseId) saleLines = saleLines.Where(x => x.Sale.TradeCaseId == tradeCaseId);
        if (request.From.HasValue) saleLines = saleLines.Where(x => x.Sale.CreatedAt >= request.From.Value);
        if (request.To.HasValue) saleLines = saleLines.Where(x => x.Sale.CreatedAt < request.To.Value);
        var sold = await saleLines.GroupBy(x => new { x.VariantId, x.Variant.Product.Name, Unit = x.Variant.Product.Unit.ShortName })
            .Select(x => new { x.Key.VariantId, x.Key.Name, x.Key.Unit, Quantity = x.Sum(line => line.Quantity), Amount = x.Sum(line => line.Quantity * line.UnitPrice), Settled = x.Where(line => line.Sale.TradeCaseId != null).Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);
        foreach (var value in sold)
        {
            var row = Row(value.VariantId, value.Name, value.Unit);
            row.Sold += value.Quantity;
            row.CustodySettled += value.Settled;
            row.Charged += value.Amount;
        }

        var returnLines = db.CustomerReturnLines.AsNoTracking()
            .Where(x => x.Document.CustomerId == request.CustomerId && x.Document.Status == BusinessDocumentStatus.Posted);
        if (request.BranchId is { } returnBranch) returnLines = returnLines.Where(x => x.Document.BranchId == returnBranch);
        else if (!currentUser.CanAccessAllBranches) returnLines = returnLines.Where(x => currentUser.BranchIds.Contains(x.Document.BranchId));
        if (request.TradeCaseId is { } returnCase) returnLines = returnLines.Where(x => x.Document.Sale.TradeCaseId == returnCase);
        if (request.From.HasValue) returnLines = returnLines.Where(x => x.Document.CreatedAt >= request.From.Value);
        if (request.To.HasValue) returnLines = returnLines.Where(x => x.Document.CreatedAt < request.To.Value);
        var returnedSales = await returnLines.GroupBy(x => new { x.VariantId, x.Variant.Product.Name, Unit = x.Variant.Product.Unit.ShortName })
            .Select(x => new { x.Key.VariantId, x.Key.Name, x.Key.Unit, Quantity = x.Sum(line => line.Quantity), Amount = x.Sum(line => line.LineAmount) })
            .ToListAsync(cancellationToken);
        foreach (var value in returnedSales)
        {
            var row = Row(value.VariantId, value.Name, value.Unit);
            row.SaleReturned += value.Quantity;
            row.Charged -= value.Amount;
        }

        var issues = db.GoodsIssueLines.AsNoTracking()
            .Where(x => x.Document.CustomerId == request.CustomerId && x.Document.Status == BusinessDocumentStatus.Posted);
        if (request.BranchId is { } issueBranch) issues = issues.Where(x => x.Document.BranchId == issueBranch);
        else if (!currentUser.CanAccessAllBranches) issues = issues.Where(x => currentUser.BranchIds.Contains(x.Document.BranchId));
        if (request.TradeCaseId is { } issueCase) issues = issues.Where(x => x.Document.TradeCaseId == issueCase);
        if (request.From.HasValue) issues = issues.Where(x => x.Document.CreatedAt >= request.From.Value);
        if (request.To.HasValue) issues = issues.Where(x => x.Document.CreatedAt < request.To.Value);
        var issued = await issues.GroupBy(x => new { x.VariantId, x.Variant.Product.Name, Unit = x.Variant.Product.Unit.ShortName })
            .Select(x => new { x.Key.VariantId, x.Key.Name, x.Key.Unit, Quantity = x.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);
        foreach (var value in issued) Row(value.VariantId, value.Name, value.Unit).CustodyIssued += value.Quantity;

        var goodsReturnLines = db.GoodsReturnLines.AsNoTracking()
            .Where(x => x.Document.CustomerId == request.CustomerId && x.Document.Status == BusinessDocumentStatus.Posted);
        if (request.BranchId is { } goodsReturnBranch) goodsReturnLines = goodsReturnLines.Where(x => x.Document.BranchId == goodsReturnBranch);
        else if (!currentUser.CanAccessAllBranches) goodsReturnLines = goodsReturnLines.Where(x => currentUser.BranchIds.Contains(x.Document.BranchId));
        if (request.TradeCaseId is { } goodsReturnCase) goodsReturnLines = goodsReturnLines.Where(x => x.Document.TradeCaseId == goodsReturnCase);
        if (request.From.HasValue) goodsReturnLines = goodsReturnLines.Where(x => x.Document.CreatedAt >= request.From.Value);
        if (request.To.HasValue) goodsReturnLines = goodsReturnLines.Where(x => x.Document.CreatedAt < request.To.Value);
        var returnedGoods = await goodsReturnLines.GroupBy(x => new { x.VariantId, x.Variant.Product.Name, Unit = x.Variant.Product.Unit.ShortName })
            .Select(x => new { x.Key.VariantId, x.Key.Name, x.Key.Unit, Quantity = x.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);
        foreach (var value in returnedGoods) Row(value.VariantId, value.Name, value.Unit).CustodyReturned += value.Quantity;

        return map.Values.OrderBy(x => x.Name).Select(x => new CustomerStatementProductDto(
            x.VariantId, x.Name, x.Unit, x.Sold, x.SaleReturned, x.Sold - x.SaleReturned,
            x.CustodyIssued, x.CustodyReturned, x.CustodySettled,
            x.CustodyIssued - x.CustodyReturned - x.CustodySettled, x.Charged)).ToList();
    }

    private IQueryable<T> Scope<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, long>> branch,
        System.Linq.Expressions.Expression<Func<T, long?>> tradeCase,
        System.Linq.Expressions.Expression<Func<T, DateTime>> occurredAt,
        GetCustomerStatementQuery request)
    {
        if (request.BranchId.HasValue)
        {
            var id = request.BranchId.Value;
            query = query.Where(Replace(branch, value => value == id));
        }
        // Branch filtering for each strongly typed query is handled by global branch filters.
        if (request.TradeCaseId.HasValue)
        {
            var id = request.TradeCaseId.Value;
            query = query.Where(Replace(tradeCase, value => value == id));
        }
        if (request.From.HasValue)
        {
            var from = request.From.Value;
            query = query.Where(Replace(occurredAt, value => value >= from));
        }
        if (request.To.HasValue)
        {
            var to = request.To.Value;
            query = query.Where(Replace(occurredAt, value => value < to));
        }
        return query;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Replace<T, TValue>(
        System.Linq.Expressions.Expression<Func<T, TValue>> selector,
        System.Linq.Expressions.Expression<Func<TValue, bool>> predicate)
    {
        var body = new ReplaceParameterVisitor(predicate.Parameters[0], selector.Body).Visit(predicate.Body)!;
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
    }

    private sealed class ReplaceParameterVisitor(
        System.Linq.Expressions.ParameterExpression source,
        System.Linq.Expressions.Expression replacement) : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node) =>
            node == source ? replacement : base.VisitParameter(node);
    }

    private static void AddBalance(Dictionary<string, decimal> balances, AccountRow account, decimal accountDelta)
    {
        var financialDelta = account.Type == AccountType.Debt ? accountDelta : -accountDelta;
        balances[account.Currency] = balances.GetValueOrDefault(account.Currency) + financialDelta;
    }

    private static string Summary(OperationType operation) => operation switch
    {
        OperationType.DebtCharge => "Qarz hisoblandi",
        OperationType.DebtPay => "Qarz to'landi",
        OperationType.CustomerPayment => "Mijoz to'lovi",
        OperationType.CustomerAdvance => "Mijoz avansi",
        OperationType.CustomerCredit => "Mijoz haqdorligi",
        OperationType.CustomerRefund => "Mijozga pul qaytarildi",
        OperationType.SaleReturn => "Mahsulot qaytarildi",
        _ => operation.ToString()
    };
}
