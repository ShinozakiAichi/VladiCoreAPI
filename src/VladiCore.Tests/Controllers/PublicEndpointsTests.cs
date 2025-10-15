using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using VladiCore.Api.Controllers;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Services;
using VladiCore.PcBuilder.Services;
using VladiCore.Recommendations.Services;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class PublicEndpointsTests
{
    [Test]
    public async Task GetProducts_ShouldReturnOk()
    {
        using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 1,
            Sku = "SKU-001",
            Name = "Test Product",
            CategoryId = 1,
            Price = 10,
            Stock = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.SaveChanges();

        var controller = new ProductsController(
            context,
            CreateCacheProvider(),
            new SlidingWindowRateLimiter(),
            new StubPriceHistoryService(),
            new StubRecommendationService());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var actionResult = await controller.GetProducts();

        actionResult.Result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task CreateReview_ShouldPersistReview()
    {
        using var context = CreateContext();
        context.Products.Add(new Product
        {
            Id = 5,
            Sku = "SKU-005",
            Name = "GPU",
            CategoryId = 2,
            Price = 500,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new ReviewsController(
            context,
            CreateCacheProvider(),
            new SlidingWindowRateLimiter(),
            Options.Create(new ReviewOptions { RequireAuthentication = false, UserEditWindowHours = 24 }),
            Options.Create(new S3Options { CdnBaseUrl = "https://cdn.example.com" }),
            new StubRatingService());

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        httpContext.Request.Headers.UserAgent = "UnitTest/1.0";
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateReviewRequest
        {
            Rating = 5,
            Text = "Отличный товар, рекомендую!",
            Title = "Лучшая покупка"
        };

        var result = await controller.CreateReview(5, request, CancellationToken.None);

        result.Result.Should().BeOfType<CreatedResult>();
        var stored = await context.ProductReviews.SingleAsync();
        stored.Rating.Should().Be(5);
        stored.Status.Should().Be(ReviewStatus.Pending);
        stored.Text.Should().Contain("Отличный товар");
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

    private sealed class StubPriceHistoryService : IPriceHistoryService
    {
        public Task<IReadOnlyCollection<PricePointDto>> GetSeriesAsync(int productId, DateTime from, DateTime to, string bucket)
        {
            IReadOnlyCollection<PricePointDto> data = Array.Empty<PricePointDto>();
            return Task.FromResult(data);
        }
    }

    private sealed class StubRecommendationService : IRecommendationService
    {
        public Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(int productId, int take, int skip)
        {
            IReadOnlyList<RecommendationDto> data = Array.Empty<RecommendationDto>();
            return Task.FromResult(data);
        }
    }

    private sealed class StubRatingService : IRatingService
    {
        public Task RecomputeAsync(int productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
