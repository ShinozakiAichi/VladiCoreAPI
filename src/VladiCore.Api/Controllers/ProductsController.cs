using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Recommendations.Services;

namespace VladiCore.Api.Controllers;

[Route("products")]
public class ProductsController : BaseApiController
{
    private readonly IPriceHistoryService _priceHistoryService;
    private readonly IRecommendationService _recommendationService;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    public ProductsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IPriceHistoryService priceHistoryService,
        IRecommendationService recommendationService)
        : base(dbContext, cache, rateLimiter)
    {
        _priceHistoryService = priceHistoryService;
        _recommendationService = recommendationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] double? minRating,
        [FromQuery] string? search,
        [FromQuery] string sort = "-created",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        sort = string.IsNullOrWhiteSpace(sort) ? "-created" : sort.Trim().ToLowerInvariant();

        var cacheKey = $"products:list:{categoryId}:{minPrice}:{maxPrice}:{minRating}:{search}:{sort}:{page}:{pageSize}";
        var cached = await Cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(2), async () =>
        {
            IQueryable<Product> query = DbContext.Products.AsNoTracking().Include(p => p.Images);

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            if (minRating.HasValue)
            {
                query = query.Where(p => p.RatingsCount == 0 || p.AverageRating >= (decimal)minRating.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
            }

            query = sort switch
            {
                "price" => query.OrderBy(p => p.Price),
                "-price" => query.OrderByDescending(p => p.Price),
                "rating" => query.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.RatingsCount),
                "-rating" => query.OrderBy(p => p.AverageRating).ThenBy(p => p.RatingsCount),
                "created" => query.OrderBy(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var skip = (page - 1) * pageSize;
            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var dtos = items.Select(ToDto).ToList();
            return new PagedResult<ProductDto>
            {
                Items = dtos,
                Total = total,
                Skip = skip,
                Take = pageSize
            };
        }).ConfigureAwait(false);

        var etag = HashUtility.Compute(System.Text.Json.JsonSerializer.Serialize(cached));
        return CachedOk(cached, etag, TimeSpan.FromMinutes(1));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"products:{id}";
        var dto = await Cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), async () =>
        {
            var product = await DbContext.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                .ConfigureAwait(false);
            return product == null ? null : ToDto(product);
        }).ConfigureAwait(false);

        if (dto == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Product not found"));
        }

        var etag = HashUtility.Compute(System.Text.Json.JsonSerializer.Serialize(dto));
        return CachedOk(dto, etag, TimeSpan.FromMinutes(5));
    }

    [HttpGet("{id:int}/price-history")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyCollection<PricePointDto>>> GetPriceHistory(
        int id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string bucket = "day",
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        if (fromDate >= toDate)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "`from` must be earlier than `to`."));
        }

        var series = await _priceHistoryService.GetSeriesAsync(id, fromDate, toDate, bucket).ConfigureAwait(false);
        return Ok(series);
    }

    [HttpGet("{id:int}/recommendations")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetRecommendations(
        int id,
        [FromQuery] int take = 6,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 20);
        skip = Math.Max(0, skip);
        var recommendations = await Cache.GetOrCreateAsync(
            $"reco:{id}:{take}:{skip}",
            TimeSpan.FromMinutes(10),
            () => _recommendationService.GetRecommendationsAsync(id, take, skip)).ConfigureAwait(false);

        return Ok(recommendations);
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
            Stock = product.Stock,
            Specs = DeserializeSpecs(product.Specs),
            Description = product.Description,
            AverageRating = (double)product.AverageRating,
            RatingsCount = product.RatingsCount,
            Photos = product.Images
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Select(image => new ProductImageDto
                {
                    Id = image.Id,
                    Key = image.ObjectKey,
                    Url = image.Url,
                    ETag = image.ETag,
                    SortOrder = image.SortOrder,
                    CreatedAt = image.CreatedAt,
                    UpdatedAt = image.UpdatedAt
                })
                .ToList()
        };
    }

    private static Dictionary<string, object>? DeserializeSpecs(string? specs)
    {
        if (string.IsNullOrWhiteSpace(specs))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(specs, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
