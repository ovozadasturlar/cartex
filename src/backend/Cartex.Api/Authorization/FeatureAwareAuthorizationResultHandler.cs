using System.Text.Json;
using Cartex.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Cartex.Api.Authorization;

public sealed class FeatureAwareAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.User.Identity?.IsAuthenticated == true
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<FeatureRequirement>().FirstOrDefault() is { } feature)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { title = "feature_locked", feature = feature.Feature }));
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
