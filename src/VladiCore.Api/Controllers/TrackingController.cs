using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers
{
    [RoutePrefix("api/track")]
    public class TrackingController : BaseApiController
    {
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
        private const int Limit = 30;

        [HttpPost, Route("view")]
        public async Task<IHttpActionResult> TrackView(TrackViewDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Content((HttpStatusCode)422, ModelState);
            }

            var ip = HttpContext.Current?.Request?.UserHostAddress ?? "unknown";
            if (!RateLimiter.IsAllowed(ip, Limit, Window))
            {
                return Content((HttpStatusCode)429, "Rate limit exceeded");
            }

            var repository = new EfRepository<ProductView>(DbContext);
            await repository.AddAsync(new ProductView
            {
                ProductId = dto.ProductId,
                SessionId = dto.SessionId,
                UserId = dto.UserId,
                ViewedAt = DateTime.UtcNow
            });
            await repository.SaveChangesAsync();

            Cache.Remove($"reco:{dto.ProductId}:10:0");

            return StatusCode(HttpStatusCode.Accepted);
        }
    }
}
