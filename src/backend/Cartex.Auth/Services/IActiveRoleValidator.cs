namespace Cartex.Auth.Services;

public interface IActiveRoleValidator
{
    Task<bool> IsValidAsync(
        long userId,
        IReadOnlyCollection<string> tokenRoles,
        string? authorizationStamp,
        CancellationToken cancellationToken);
}
