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
using VladiCore.Api.Infrastructure.ObjectStorage;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Authorize(Policy = "Admin")]
[Route("products")]
public class AdminProductsController : BaseApiController
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IObjectStorageService _storage;

    public AdminProductsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IObjectStorageService storage)
        : base(dbContext, cache, rateLimiter)
    {
        _storage = storage;
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            Price = request.Price,
            Stock = request.Stock,
            Specs = request.Specs == null ? null : JsonSerializer.Serialize(request.Specs, SerializerOptions),
            Description = request.Description,
            AverageRating = 0m,
            RatingsCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbContext.Products.AddAsync(product, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await DbContext.ProductPriceHistory.AddAsync(new ProductPriceHistory
        {
            ProductId = product.Id,
            Price = product.Price,
            ChangedAt = now
        }, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix("products");
        Cache.RemoveByPrefix($"reco:{product.Id}");
        var dto = await DbContext.Products.AsNoTracking().Include(p => p.Images).FirstAsync(p => p.Id == product.Id, cancellationToken);
        return CreatedAtAction(nameof(ProductsController.GetProduct), "Products", new { id = product.Id }, ProductsController_ToDto(dto));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(
        int id,
        [FromBody] UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = await DbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Product not found"));
        }

        var originalPrice = product.Price;
        product.Sku = request.Sku.Trim();
        product.Name = request.Name.Trim();
        product.CategoryId = request.CategoryId;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Specs = request.Specs == null ? null : JsonSerializer.Serialize(request.Specs, SerializerOptions);
        product.Description = request.Description;
        product.UpdatedAt = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (product.Price != originalPrice)
        {
            await DbContext.ProductPriceHistory.AddAsync(new ProductPriceHistory
            {
                ProductId = product.Id,
                Price = product.Price,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        Cache.RemoveByPrefix("products");
        Cache.RemoveByPrefix($"reco:{product.Id}");
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken = default)
    {
        var product = await DbContext.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Product not found"));
        }

        DbContext.Products.Remove(product);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Cache.RemoveByPrefix("products");
        Cache.RemoveByPrefix($"reco:{id}");

        return NoContent();
    }

    [HttpPost("{id:int}/photos")]
    public async Task<ActionResult<ProductImageDto>> ConfirmPhoto(
        int id,
        [FromBody] ConfirmProductPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var product = await DbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        if (product == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Product not found"));
        }

        if (!request.Key.StartsWith($"products/{id}/", StringComparison.Ordinal))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Invalid storage key prefix."));
        }

        var photo = new ProductImage
        {
            ProductId = id,
            ObjectKey = request.Key,
            ETag = request.ETag,
            Url = _storage.BuildUrl(request.Key),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await DbContext.ProductImages.AddAsync(photo, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Cache.RemoveByPrefix($"products:{id}");
        Cache.RemoveByPrefix($"reco:{id}");

        var dto = new ProductImageDto
        {
            Id = photo.Id,
            Key = photo.ObjectKey,
            Url = photo.Url,
            ETag = photo.ETag,
            SortOrder = photo.SortOrder,
            CreatedAt = photo.CreatedAt,
            UpdatedAt = photo.UpdatedAt
        };

        return CreatedAtAction(nameof(ProductsController.GetProduct), "Products", new { id }, dto);
    }

    [HttpDelete("{productId:int}/photos/{photoId:long}")]
    public async Task<IActionResult> DeletePhoto(int productId, long photoId, CancellationToken cancellationToken = default)
    {
        var photo = await DbContext.ProductImages.FirstOrDefaultAsync(p => p.Id == photoId && p.ProductId == productId, cancellationToken).ConfigureAwait(false);
        if (photo == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Photo not found"));
        }

        DbContext.ProductImages.Remove(photo);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Cache.RemoveByPrefix($"products:{productId}");
        Cache.RemoveByPrefix($"reco:{productId}");

        return NoContent();
    }

    private static ProductDto ProductsController_ToDto(Product product)
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
