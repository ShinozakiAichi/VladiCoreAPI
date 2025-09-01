using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VladiCore.App.Exceptions;

namespace VladiCore.Api.Infrastructure;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, ex.Message);
            await WriteErrorAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, ex.Message);
            await WriteErrorAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, "internal_error", "An unexpected error occurred");
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message, object? details = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = new { code, message, details } });
        return context.Response.WriteAsync(payload);
    }
}
