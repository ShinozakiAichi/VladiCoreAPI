using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Dapper;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Models;

namespace VladiCore.Api.Controllers
{
    [Authorize]
    [RoutePrefix("api/analytics")]
    public class AnalyticsController : BaseApiController
    {
        [HttpGet, Route("top-copurchases")]
        public async Task<IHttpActionResult> GetTopCoPurchases(DateTime? from = null, DateTime? to = null, int take = 50)
        {
            var sql = @"SELECT oi.ProductId AS ProductId, p.Name, p.Price, COUNT(*) AS Count
                        FROM OrderItems oi
                        JOIN Orders o ON oi.OrderId = o.Id
                        JOIN Products p ON p.Id = oi.ProductId
                        WHERE (@From IS NULL OR o.CreatedAt >= @From) AND (@To IS NULL OR o.CreatedAt <= @To)
                        GROUP BY oi.ProductId, p.Name, p.Price
                        ORDER BY Count DESC
                        LIMIT @Take";

            using (var connection = ServiceContainer.ConnectionFactory.Create())
            {
                var items = await connection.QueryAsync<AnalyticsItemDto>(sql, new { From = from, To = to, Take = take });
                return Ok(items);
            }
        }

        [HttpGet, Route("top-views")]
        public async Task<IHttpActionResult> GetTopViews(DateTime? from = null, DateTime? to = null, int take = 50)
        {
            var sql = @"SELECT pv.ProductId, p.Name, p.Price, COUNT(*) AS Count
                        FROM ProductViews pv
                        JOIN Products p ON p.Id = pv.ProductId
                        WHERE (@From IS NULL OR pv.ViewedAt >= @From) AND (@To IS NULL OR pv.ViewedAt <= @To)
                        GROUP BY pv.ProductId, p.Name, p.Price
                        ORDER BY Count DESC
                        LIMIT @Take";

            using (var connection = ServiceContainer.ConnectionFactory.Create())
            {
                var items = await connection.QueryAsync<AnalyticsItemDto>(sql, new { From = from, To = to, Take = take });
                return Ok(items);
            }
        }
    }
}
