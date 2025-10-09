using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VladiCore.Api.Handlers
{
    /// <summary>
    /// Handles CORS preflight requests manually to inject custom headers.
    /// </summary>
    public class PreflightRequestsHandler : DelegatingHandler
    {
        private static readonly string[] AllowedOrigins =
        {
            "http://localhost:5173",
            "https://shop.example.com"
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.Method.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return base.SendAsync(request, cancellationToken);
            }

            var origin = request.Headers.GetValues("Origin").FirstOrDefault();
            if (origin != null && AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Access-Control-Allow-Origin", origin);
                response.Headers.Add("Access-Control-Allow-Credentials", "true");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
                response.Headers.Add("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
                response.Headers.Vary.Add("Origin");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }
    }
}
