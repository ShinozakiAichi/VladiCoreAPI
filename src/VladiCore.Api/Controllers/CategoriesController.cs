using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Route("categories")]
public class CategoriesController : BaseApiController
{
    public CategoriesController(AppDbContext dbContext, ICacheProvider cache, IRateLimiter rateLimiter)
        : base(dbContext, cache, rateLimiter)
    {
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken = default)
    {
        var categories = await Cache.GetOrCreateAsync(
                "categories:list",
                TimeSpan.FromMinutes(5),
                async () =>
                {
                    var items = await DbContext.Categories
                        .AsNoTracking()
                        .OrderBy(c => c.Name)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return items.Select(ToDto).ToList();
                })
            .ConfigureAwait(false);

        var etag = HashUtility.Compute(JsonSerializer.Serialize(categories));
        return CachedOk(categories, etag, TimeSpan.FromMinutes(5));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"categories:{id}";
        var category = await Cache.GetOrCreateAsync(
                cacheKey,
                TimeSpan.FromMinutes(5),
                async () =>
                {
                    var entity = await DbContext.Categories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        .ConfigureAwait(false);
                    return entity == null ? null : ToDto(entity);
                })
            .ConfigureAwait(false);

        if (category == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Category not found"));
        }

        var etag = HashUtility.Compute(JsonSerializer.Serialize(category));
        return CachedOk(category, etag, TimeSpan.FromMinutes(5));
    }

    internal static CategoryDto ToDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ParentId = category.ParentId,
            CreatedAt = category.CreatedAt
        };
    }
}
