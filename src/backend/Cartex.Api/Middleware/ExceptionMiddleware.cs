using System.Text.Json;
using Cartex.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, extensions) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                (object?)validationEx.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage })
            ),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
            DbUpdateException => (StatusCodes.Status409Conflict, "The operation conflicts with existing data. Retry.", null),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message, null),
            BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message, null),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", null),
            _ => (StatusCodes.Status500InternalServerError, "An internal error occurred", null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{statusCode}",
            ["title"] = title,
            ["status"] = statusCode
        };
        if (extensions is not null) problem["errors"] = extensions;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
