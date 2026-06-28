using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Auth.Services;

namespace Cartex.Application.Auth.Commands;

public record LoginCommand(string Username, string Password) : IRequest<LoginResponse>;

public record LoginResponse(string Token, string FullName, string Role);

public sealed class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!user.IsActive)
            throw new ForbiddenException("User is deactivated.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var permissions = user.Role.RolePermissions
            .Where(rp => rp.Permission.IsEnabled)
            .Select(rp => rp.Permission.Name)
            .ToList();

        var token = jwtTokenGenerator.GenerateToken(user.Id, user.Username, user.Role.Name, permissions);

        return new LoginResponse(token, user.FullName, user.Role.Name);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
