using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VladiCore.Api.Models.Catalog;
using VladiCore.Data;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ProductDto>>> Get()
    {
        var items = await _db.Products
            .Select(p => new ProductDto(p.Id, p.Name, p.Slug, p.Description, p.BasePrice, p.Currency.ToString(), p.IsActive, p.CategoryId))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> Get(Guid id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        return new ProductDto(p.Id, p.Name, p.Slug, p.Description, p.BasePrice, p.Currency.ToString(), p.IsActive, p.CategoryId);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            BasePrice = request.BasePrice,
            Currency = Enum.Parse<Currency>(request.Currency),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        var dto = new ProductDto(product.Id, product.Name, product.Slug, product.Description, product.BasePrice, product.Currency.ToString(), product.IsActive, product.CategoryId);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateProductRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        product.CategoryId = request.CategoryId;
        product.Name = request.Name;
        product.Slug = request.Slug;
        product.Description = request.Description;
        product.BasePrice = request.BasePrice;
        product.Currency = Enum.Parse<Currency>(request.Currency);
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
