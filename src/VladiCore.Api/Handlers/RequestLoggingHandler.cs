using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace VladiCore.Api.Handlers
{
    /// <summary>
    /// Logs HTTP requests with correlation identifiers.
    /// </summary>
    public class RequestLoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
            var stopwatch = Stopwatch.StartNew();

            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            response.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

            Log.Information("HTTP {Method} {Path} => {StatusCode} in {Elapsed} ms (corrId: {CorrelationId})",
                request.Method.Method,
                request.RequestUri,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return response;
        }
    }
}
