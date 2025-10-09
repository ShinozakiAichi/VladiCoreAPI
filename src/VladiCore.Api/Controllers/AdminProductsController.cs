using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Authorize]
[Route("api/products")]
public class AdminProductsController : BaseApiController
{
    public AdminProductsController(AppDbContext dbContext, ICacheProvider cache, IRateLimiter rateLimiter)
        : base(dbContext, cache, rateLimiter)
    {
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
}
