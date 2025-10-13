using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Infrastructure.ObjectStorage;
using VladiCore.Data.Contexts;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/products")]
public class AdminProductsController : BaseApiController
{
    private const int MaxUploadSizeBytes = 10_000_000;
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
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
    public async Task<IActionResult> Upsert([FromBody] ProductDto dto)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var repository = new EfRepository<Product>(DbContext);
        if (dto.Id == 0)
        {
            var product = new Product
            {
                Sku = dto.Sku,
                Name = dto.Name,
                CategoryId = dto.CategoryId,
                Price = dto.Price,
                OldPrice = dto.OldPrice,
                Attributes = dto.Attributes,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(product);
        }
        else
        {
            var product = await repository.FindAsync(dto.Id);
            if (product == null)
            {
                return NotFound();
            }

            product.Sku = dto.Sku;
            product.Name = dto.Name;
            product.CategoryId = dto.CategoryId;
            product.Price = dto.Price;
            product.OldPrice = dto.OldPrice;
            product.Attributes = dto.Attributes;
        }

        await repository.SaveChangesAsync();
        Cache.RemoveByPrefix("products:");
        Cache.RemoveByPrefix($"reco:{dto.Id}:");

        return NoContent();
    }

    [HttpPost("{id:int}/images/upload")]
    public async Task<IActionResult> UploadImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        if (file.Length > MaxUploadSizeBytes)
        {
            return BadRequest("File exceeds maximum allowed size (10 MB).");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Unsupported content type. Allowed: JPEG, PNG, WEBP.");
        }

        var product = await DbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        await using var inputStream = file.OpenReadStream();
        Image image;
        try
        {
            image = await Image.LoadAsync(inputStream, cancellationToken).ConfigureAwait(false);
        }
        catch (UnknownImageFormatException)
        {
            return BadRequest("Unsupported image format.");
        }

        await using var originalStream = new MemoryStream();
        await using var thumbnailStream = new MemoryStream();

        using (image)
        {
            using var originalImage = image.Clone(ctx =>
            {
                ctx.AutoOrient();
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(2048, 2048)
                });
            });

            using var thumbnailImage = image.Clone(ctx =>
            {
                ctx.AutoOrient();
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(400, 400)
                });
            });

            await originalImage.SaveAsJpegAsync(originalStream, new JpegEncoder { Quality = 90 }, cancellationToken).ConfigureAwait(false);
            await thumbnailImage.SaveAsJpegAsync(thumbnailStream, new JpegEncoder { Quality = 85 }, cancellationToken).ConfigureAwait(false);
        }

        originalStream.Position = 0;
        thumbnailStream.Position = 0;

        var baseKey = $"products/{id}/{Guid.NewGuid():N}";
        var originalKey = $"{baseKey}.jpg";
        var thumbnailKey = $"{baseKey}_thumb.jpg";

        var originalUrl = await _storage.UploadAsync(originalKey, originalStream, "image/jpeg", cancellationToken).ConfigureAwait(false);
        var thumbnailUrl = await _storage.UploadAsync(thumbnailKey, thumbnailStream, "image/jpeg", cancellationToken).ConfigureAwait(false);

        var productImage = new ProductImage
        {
            ProductId = id,
            ObjectKey = originalKey,
            ThumbnailKey = thumbnailKey,
            Url = originalUrl,
            ThumbnailUrl = thumbnailUrl,
            CreatedAt = DateTime.UtcNow
        };

        await DbContext.ProductImages.AddAsync(productImage, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix("products:");

        var response = new ProductImageDto
        {
            Id = productImage.Id,
            Key = productImage.ObjectKey,
            Url = productImage.Url,
            ThumbnailUrl = productImage.ThumbnailUrl,
            CreatedAt = productImage.CreatedAt
        };

        return Ok(response);
    }
}
