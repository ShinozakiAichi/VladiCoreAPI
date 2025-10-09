using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Models;
using VladiCore.Data.Contexts;
using VladiCore.Data.Infrastructure;

namespace VladiCore.Api.Controllers;

[Authorize]
[Route("api/analytics")]
public class AnalyticsController : BaseApiController
{
    private readonly IMySqlConnectionFactory _connectionFactory;

    public AnalyticsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IMySqlConnectionFactory connectionFactory)
        : base(dbContext, cache, rateLimiter)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("top-copurchases")]
    public async Task<IActionResult> GetTopCoPurchases(DateTime? from = null, DateTime? to = null, int take = 50)
    {
        const string sql = @"SELECT oi.ProductId AS ProductId, p.Name, p.Price, COUNT(*) AS Count
                              FROM OrderItems oi
                              JOIN Orders o ON oi.OrderId = o.Id
                              JOIN Products p ON p.Id = oi.ProductId
                              WHERE (@From IS NULL OR o.CreatedAt >= @From) AND (@To IS NULL OR o.CreatedAt <= @To)
                              GROUP BY oi.ProductId, p.Name, p.Price
                              ORDER BY Count DESC
                              LIMIT @Take";

        using var connection = _connectionFactory.Create();
        var items = await connection.QueryAsync<AnalyticsItemDto>(sql, new { From = from, To = to, Take = take });
        return Ok(items);
    }

    [HttpGet("top-views")]
    public async Task<IActionResult> GetTopViews(DateTime? from = null, DateTime? to = null, int take = 50)
    {
        const string sql = @"SELECT pv.ProductId, p.Name, p.Price, COUNT(*) AS Count
                              FROM ProductViews pv
                              JOIN Products p ON p.Id = pv.ProductId
                              WHERE (@From IS NULL OR pv.ViewedAt >= @From) AND (@To IS NULL OR pv.ViewedAt <= @To)
                              GROUP BY pv.ProductId, p.Name, p.Price
                              ORDER BY Count DESC
                              LIMIT @Take";

        using var connection = _connectionFactory.Create();
        var items = await connection.QueryAsync<AnalyticsItemDto>(sql, new { From = from, To = to, Take = take });
        return Ok(items);
    }
}
