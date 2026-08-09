using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Business.Commands;

public record UpdateBusinessCommand(string Name, string? LegalName, string Currency, string? Phone = null, string? Address = null, string? LogoImageKey = null, string? Telegram = null, string? Website = null, string? MonochromeLogoImageKey = null) : ICommand<Unit>;

public sealed class UpdateBusinessCommandHandler(IApplicationDbContext db, IAuditService audit)
    : IRequestHandler<UpdateBusinessCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBusinessCommand request, CancellationToken cancellationToken)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        business.Name = request.Name.Trim();
        business.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
        business.Currency = request.Currency.Trim().ToUpperInvariant();
        business.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        business.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        business.LogoImageKey = string.IsNullOrWhiteSpace(request.LogoImageKey) ? null : request.LogoImageKey.Trim();
        business.MonochromeLogoImageKey = string.IsNullOrWhiteSpace(request.MonochromeLogoImageKey)
            ? null
            : request.MonochromeLogoImageKey.Trim();
        business.Telegram = string.IsNullOrWhiteSpace(request.Telegram) ? null : request.Telegram.Trim();
        business.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();

        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("business.updated", "businesses", business.Id, new
        {
            business.Name,
            business.LegalName,
            business.Currency,
            business.Phone,
            business.Address,
            business.LogoImageKey,
            business.MonochromeLogoImageKey
        }, "Tashkilot ma'lumotlari yangilandi");
        return Unit.Value;
    }
}

public sealed class UpdateBusinessCommandValidator : AbstractValidator<UpdateBusinessCommand>
{
    public UpdateBusinessCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.LogoImageKey).MaximumLength(500);
        RuleFor(x => x.MonochromeLogoImageKey).MaximumLength(500);
    }
}
