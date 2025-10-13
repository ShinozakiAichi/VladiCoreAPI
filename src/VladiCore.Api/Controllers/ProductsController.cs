using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Recommendations.Services;

namespace VladiCore.Api.Controllers;

[Route("api/products")]
public class ProductsController : BaseApiController
{
    private readonly IPriceHistoryService _priceHistoryService;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IPriceHistoryService priceHistoryService,
        IRecommendationService recommendationService,
        ILogger<ProductsController> logger)
        : base(dbContext, cache, rateLimiter)
    {
        _priceHistoryService = priceHistoryService;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetProducts(int? categoryId = null, string? q = null, string sort = "price", int skip = 0, int take = 20)
    {
        skip = Math.Max(0, skip);
        take = Math.Max(1, Math.Min(100, take));

        var cacheKey = $"products:{categoryId}:{q}:{sort}:{skip}:{take}";
        object result;

        try
        {
            result = Cache.GetOrCreate(cacheKey, TimeSpan.FromSeconds(30), () =>
            {
                var repository = new EfRepository<Product>(DbContext);
                IQueryable<Product> query = repository.Query().AsNoTracking();
                query = query.Include(p => p.Images);

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = query.Where(p => p.Name.Contains(q));
                }

                query = sort switch
                {
                    "price" => query.OrderBy(p => p.Price),
                    "-price" => query.OrderByDescending(p => p.Price),
                    "name" => query.OrderBy(p => p.Name),
                    _ => query.OrderBy(p => p.Id)
                };

                var total = query.Count();
                var items = query
                    .Skip(skip)
                    .Take(take)
                    .ToList()
                    .Select(ToDto)
                    .ToList();

                return new { total, items };
            });
        }
        catch (MySqlException ex) when (IsMissingTableError(ex))
        {
            _logger.LogError(ex, "Product catalog storage is not initialized");
            return Problem(
                title: "Product catalog unavailable",
                detail: "Product catalog data is temporarily unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var etag = HashUtility.Compute(System.Text.Json.JsonSerializer.Serialize(result));
        return CachedOk(result, etag, TimeSpan.FromSeconds(60));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProduct(int id)
    {
        var repository = new EfRepository<Product>(DbContext);
        var query = repository.Query().AsNoTracking().Include(p => p.Images);
        var product = await query.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            return NotFound();
        }

        var etag = HashUtility.Compute($"product:{product.Id}:{product.Price}:{product.UpdatedAt()}");
        return CachedOk(ToDto(product), etag, TimeSpan.FromSeconds(60));
    }

    [HttpGet("{id:int}/price-history")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPriceHistory(int id, DateTime? from = null, DateTime? to = null, string bucket = "day")
    {
        var now = DateTime.UtcNow;
        var fromDate = from ?? now.AddDays(-30);
        var toDate = to ?? now;

        var series = await _priceHistoryService.GetSeriesAsync(id, fromDate, toDate, bucket);
        var etag = HashUtility.Compute($"history:{id}:{fromDate:O}:{toDate:O}:{bucket}:{series.Count}");
        return CachedOk(series, etag, TimeSpan.FromSeconds(60));
    }

    [HttpGet("{id:int}/recommendations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecommendations(int id, int take = 10, int skip = 0)
    {
        var cacheKey = $"reco:{id}:{take}:{skip}";
        var recommendations = await Cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), () =>
            _recommendationService.GetRecommendationsAsync(id, take, skip));

        var etag = HashUtility.Compute($"reco:{id}:{take}:{skip}:{recommendations.Count}");
        return CachedOk(recommendations, etag, TimeSpan.FromSeconds(300));
    }

    private static bool IsMissingTableError(MySqlException exception)
    {
        const int noSuchTableErrorCode = 1146;
        return exception.Number == noSuchTableErrorCode;
    }

    private static ProductDto ToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            CategoryId = product.CategoryId,
            Price = product.Price,
            OldPrice = product.OldPrice,
            Attributes = product.Attributes,
            Images = product.Images
                .OrderByDescending(i => i.CreatedAt)
                .Select(image => new ProductImageDto
                {
                    Id = image.Id,
                    Key = image.ObjectKey,
                    Url = image.Url,
                    ThumbnailUrl = image.ThumbnailUrl,
                    CreatedAt = image.CreatedAt
                })
                .ToList()
        };
    }
}

internal static class ProductExtensions
{
    public static string UpdatedAt(this Product product)
    {
        var imageStamp = string.Join('|', product.Images.OrderBy(i => i.Id).Select(i => i.ObjectKey));
        return $"{product.Price}:{product.OldPrice}:{product.Attributes}:{imageStamp}";
    }
}
