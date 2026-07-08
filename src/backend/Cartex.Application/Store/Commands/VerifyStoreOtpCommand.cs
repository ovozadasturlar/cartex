using Cartex.Application.Common;
using Cartex.Application.Common.Messaging;
using Cartex.Auth.Services;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record VerifyStoreOtpCommand(string Phone, string Code, string? DeviceName = null) : IRequest<StoreLoginResponse>;

public sealed class VerifyStoreOtpCommandHandler(
    IApplicationDbContext db,
    IFeatureStateProvider features,
    IPasswordHasher passwordHasher,
    StoreTokenBuilder tokenBuilder) : IRequestHandler<VerifyStoreOtpCommand, StoreLoginResponse>
{
    public async Task<StoreLoginResponse> Handle(VerifyStoreOtpCommand request, CancellationToken cancellationToken)
    {
        if (!await features.IsEnabledAsync(FeatureCatalog.Ordering, cancellationToken))
            throw new ForbiddenException("Onlayn buyurtma o'chirilgan.");

        var phone = Phones.Normalize(request.Phone);
        var customer = (phone is null ? null : await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, cancellationToken))
            ?? throw new UnauthorizedAccessException("Kod noto'g'ri yoki eskirgan.");

        var now = DateTime.UtcNow;
        var challenge = await db.OtpChallenges
            .Where(o => o.CustomerId == customer.Id && o.ConsumedAt == null)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null || challenge.ExpiresAt <= now || challenge.Attempts >= 5)
            throw new UnauthorizedAccessException("Kod noto'g'ri yoki eskirgan.");

        if (!passwordHasher.Verify(request.Code.Trim(), challenge.CodeHash))
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Kod noto'g'ri yoki eskirgan.");
        }

        challenge.ConsumedAt = now;
        return await tokenBuilder.IssueAsync(customer, request.DeviceName, cancellationToken);
    }
}

public sealed class VerifyStoreOtpCommandValidator : AbstractValidator<VerifyStoreOtpCommand>
{
    public VerifyStoreOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
    }
}
