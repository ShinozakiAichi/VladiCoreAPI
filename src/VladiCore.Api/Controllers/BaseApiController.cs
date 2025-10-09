using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;

namespace VladiCore.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected BaseApiController(AppDbContext dbContext, ICacheProvider cache, IRateLimiter rateLimiter)
    {
        DbContext = dbContext;
        Cache = cache;
        RateLimiter = rateLimiter;
    }

    protected AppDbContext DbContext { get; }

    protected ICacheProvider Cache { get; }

    protected IRateLimiter RateLimiter { get; }

    protected IActionResult CachedOk(object value, string etag, TimeSpan ttl)
    {
        var headers = Response.GetTypedHeaders();
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = ttl
        };
        headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");

        return Ok(value);
    }
}
