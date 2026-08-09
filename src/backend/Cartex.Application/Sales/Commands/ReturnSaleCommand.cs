using Cartex.Application.CustomerReturns.Commands;
using Cartex.Domain.Enums;
using FluentValidation;

namespace Cartex.Application.Sales.Commands;

public record ReturnLineDto(long SaleItemId, decimal Quantity, bool Restock, string? Reason);

/// <summary>
/// Compatibility command for existing clients. New clients should use
/// CreateCustomerReturnCommand and explicitly show the disposition/settlement review.
/// </summary>
public record ReturnSaleCommand(long SaleId, List<ReturnLineDto> Lines) : ICommand<Unit>;

public sealed class ReturnSaleCommandHandler(ISender sender) : IRequestHandler<ReturnSaleCommand, Unit>
{
    public async Task<Unit> Handle(ReturnSaleCommand request, CancellationToken cancellationToken)
    {
        await sender.Send(new CreateCustomerReturnCommand(
            request.SaleId,
            request.Lines.Select(x => new CustomerReturnLineInput(
                x.SaleItemId,
                x.Quantity,
                x.Reason,
                x.Restock ? ReturnItemCondition.Sellable : ReturnItemCondition.Opened,
                x.Restock ? InventoryDisposition.SellableRestock : InventoryDisposition.Quarantine)).ToList(),
            AutoSettle: true), cancellationToken);
        return Unit.Value;
    }
}

public sealed class ReturnSaleCommandValidator : AbstractValidator<ReturnSaleCommand>
{
    public ReturnSaleCommandValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).Must(x => x.Quantity > 0)
            .WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
    }
}
