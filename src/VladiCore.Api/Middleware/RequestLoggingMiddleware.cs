using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace VladiCore.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        context.Items["CorrelationId"] = correlationId;
        context.Request.Headers.TryAdd("X-Correlation-Id", correlationId);

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            }

            if (!context.Response.Headers.ContainsKey("X-Request-Id"))
            {
                context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            }

            return Task.CompletedTask;
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} => {StatusCode} in {Elapsed} ms (corrId: {CorrelationId})",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
}
