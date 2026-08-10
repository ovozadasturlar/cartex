using System.Text.Json;
using Cartex.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        if (exception is OperationCanceledException || context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request was canceled by the client: {Path}", context.Request.Path);
            return;
        }

        var (statusCode, code, title, extensions) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "validation_error",
                "Validation failed",
                (object?)validationEx.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage })
            ),
            NotFoundException domainEx => (StatusCodes.Status404NotFound, domainEx.Code, exception.Message, null),
            ConflictException domainEx => (StatusCodes.Status409Conflict, domainEx.Code, exception.Message, null),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "unique_conflict", "The operation conflicts with existing data. Retry.", null),
            DbUpdateException => (StatusCodes.Status500InternalServerError, "database_error", "An internal data error occurred", null),
            ForbiddenException domainEx => (StatusCodes.Status403Forbidden, domainEx.Code, exception.Message, null),
            BusinessRuleException domainEx => (StatusCodes.Status400BadRequest, domainEx.Code, exception.Message, null),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Unauthorized", null),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An internal error occurred", null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{statusCode}",
            ["title"] = title,
            ["status"] = statusCode,
            ["code"] = code,
            ["correlationId"] = context.TraceIdentifier
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
