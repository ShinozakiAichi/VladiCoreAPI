using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using VladiCore.Api.Controllers;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class CategoryControllerTests
{
    [Test]
    public async Task CreateCategory_ShouldBeRetrievableFromPublicEndpoint()
    {
        using var context = CreateContext();
        var cache = CreateCacheProvider();
        var rateLimiter = new SlidingWindowRateLimiter();

        var adminController = new AdminCategoriesController(context, cache, rateLimiter)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var publicController = new CategoriesController(context, cache, rateLimiter)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new UpsertCategoryRequest { Name = "Components" };
        var result = await adminController.CreateCategory(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(CategoriesController.GetCategory));

        var listResult = await publicController.GetCategories(CancellationToken.None);
        var ok = listResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var categories = ok.Value.Should().BeAssignableTo<IReadOnlyCollection<CategoryDto>>().Subject;
        categories.Should().ContainSingle();
        categories.Should().Contain(c => c.Name == "Components");
    }

    [Test]
    public async Task DeleteCategory_WithChildren_ShouldReturnConflict()
    {
        using var context = CreateContext();
        context.Categories.Add(new Category
        {
            Id = 1,
            Name = "Root",
            CreatedAt = DateTime.UtcNow
        });
        context.Categories.Add(new Category
        {
            Id = 2,
            Name = "Child",
            ParentId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var adminController = new AdminCategoriesController(context, CreateCacheProvider(), new SlidingWindowRateLimiter())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await adminController.DeleteCategory(1, CancellationToken.None);
        result.Should().BeOfType<ConflictObjectResult>();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ICacheProvider CreateCacheProvider()
    {
        return new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
    }
}
