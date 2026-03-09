using System.Text.Json;
using FluentValidation;

namespace Cartex.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next)
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

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                new
                {
                    message = "Validation failed",
                    errors = validationEx.Errors
                        .Select(e => new { field = e.PropertyName, error = e.ErrorMessage })
                }
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                (object)new { message = exception.Message }
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                (object)new { message = "Unauthorized" }
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                (object)new { message = "An internal server error occurred" }
            )
        };

        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
