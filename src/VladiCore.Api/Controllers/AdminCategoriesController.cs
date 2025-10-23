using System;
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

[Authorize(Policy = "Admin")]
[Route("categories")]
public class AdminCategoriesController : BaseApiController
{
    public AdminCategoriesController(AppDbContext dbContext, ICacheProvider cache, IRateLimiter rateLimiter)
        : base(dbContext, cache, rateLimiter)
    {
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] UpsertCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Name cannot be empty."));
        }

        if (request.ParentId.HasValue)
        {
            var parentExists = await DbContext.Categories.AnyAsync(c => c.Id == request.ParentId.Value, cancellationToken).ConfigureAwait(false);
            if (!parentExists)
            {
                return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Parent category not found."));
            }
        }

        var category = new Category
        {
            Name = name,
            ParentId = request.ParentId,
            CreatedAt = DateTime.UtcNow
        };

        await DbContext.Categories.AddAsync(category, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix("categories");
        var dto = CategoriesController.ToDto(category);
        return CreatedAtAction(nameof(CategoriesController.GetCategory), "Categories", new { id = category.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(
        int id,
        [FromBody] UpsertCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await DbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
        if (category == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Category not found"));
        }

        if (request.ParentId == id)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Category cannot be its own parent."));
        }

        if (request.ParentId.HasValue)
        {
            var parentExists = await DbContext.Categories.AnyAsync(c => c.Id == request.ParentId.Value, cancellationToken).ConfigureAwait(false);
            if (!parentExists)
            {
                return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Parent category not found."));
            }
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest, "Name cannot be empty."));
        }

        category.Name = name;
        category.ParentId = request.ParentId;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Cache.RemoveByPrefix("categories");
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken = default)
    {
        var category = await DbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
        if (category == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Category not found"));
        }

        var hasChildren = await DbContext.Categories.AnyAsync(c => c.ParentId == id, cancellationToken).ConfigureAwait(false);
        if (hasChildren)
        {
            return Conflict(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status409Conflict, "Category has child categories."));
        }

        var hasProducts = await DbContext.Products.AnyAsync(p => p.CategoryId == id, cancellationToken).ConfigureAwait(false);
        if (hasProducts)
        {
            return Conflict(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status409Conflict, "Category contains products."));
        }

        DbContext.Categories.Remove(category);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix("categories");
        return NoContent();
    }
}
