using Cartex.Application.Common.Interfaces;
using Cartex.Application.Customers.Queries;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Customers.Commands;

public sealed record ExportCustomerStatementCommand(
    long CustomerId,
    string Format,
    string Mode,
    DateTime? From = null,
    DateTime? To = null,
    long? BranchId = null,
    string? DocumentTypes = null) : ICommand<GeneratedDocument>;

public sealed class ExportCustomerStatementCommandHandler(
    ICurrentUser currentUser,
    ISender sender,
    ICustomerStatementExporter exporter,
    IAuditService audit) : IRequestHandler<ExportCustomerStatementCommand, GeneratedDocument>
{
    public async Task<GeneratedDocument> Handle(
        ExportCustomerStatementCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Statements.Export))
            throw new ForbiddenException("Hisob ko'chirmasini eksport qilishga ruxsat yo'q.");
        var statement = await sender.Send(new GetCustomerStatementQuery(
            request.CustomerId, request.From, request.To,
            request.BranchId, request.DocumentTypes), cancellationToken);
        var document = exporter.Export(statement, request.Format, request.Mode);
        audit.SetOutcome("statement.exported", "customers", request.CustomerId, new
        {
            request.Format, request.Mode, request.From, request.To,
            request.BranchId, request.DocumentTypes,
            document.FileName, bytes = document.Content.Length
        }, "Mijoz hisob ko'chirmasi eksport qilindi", request.BranchId);
        return document;
    }
}

public sealed class ExportCustomerStatementCommandValidator : AbstractValidator<ExportCustomerStatementCommand>
{
    public ExportCustomerStatementCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Format).Must(x => x is not null &&
            (x.Equals("pdf", StringComparison.OrdinalIgnoreCase)
             || x.Equals("xlsx", StringComparison.OrdinalIgnoreCase)));
        RuleFor(x => x.Mode).Must(x => x is not null &&
            (x.Equals("timeline", StringComparison.OrdinalIgnoreCase)
             || x.Equals("consolidated", StringComparison.OrdinalIgnoreCase)
             || x.Equals("both", StringComparison.OrdinalIgnoreCase)));
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
        RuleFor(x => x.DocumentTypes).MaximumLength(500);
    }
}
