using System;
using System.Net.Http;
using System.Web.Http;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;

namespace VladiCore.Api.Controllers
{
    public abstract class BaseApiController : ApiController
    {
        private VladiCoreContext _context;

        protected VladiCoreContext DbContext => _context ?? (_context = ServiceContainer.CreateContext());
        protected ICacheProvider Cache => ServiceContainer.Cache;
        protected IRateLimiter RateLimiter => ServiceContainer.RateLimiter;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected static HttpResponseMessage CreateCachedResponse(HttpRequestMessage request, object data, string etag, TimeSpan ttl)
        {
            var response = request.CreateResponse(System.Net.HttpStatusCode.OK, data);
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = ttl
            };
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{etag}\"");
            return response;
        }
    }
}
