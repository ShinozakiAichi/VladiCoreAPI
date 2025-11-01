using System.Collections.Generic;
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
using VladiCore.PcBuilder.Exceptions;
using VladiCore.PcBuilder.Services;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class PcControllerTests
{
    [Test]
    public async Task AutoBuild_ShouldReturn422_WhenBuilderCannotAssembleConfiguration()
    {
        await using var context = CreateContext();
        var cache = CreateCacheProvider();
        var rateLimiter = new SlidingWindowRateLimiter();

        var controller = new PcController(
            context,
            cache,
            rateLimiter,
            new NoOpCompatibilityService(),
            new FailingAutoBuilderService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new AutoBuildRequest
        {
            Budget = 1000,
            Priorities = new List<string> { "gaming" },
            Platform = "intel"
        };

        var result = await controller.AutoBuild(request);

        var unprocessable = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocessable.Value.Should().BeEquivalentTo(new { error = FailingAutoBuilderService.ExpectedMessage });
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("pc-controller-tests")
            .Options;
        return new AppDbContext(options);
    }

    private static ICacheProvider CreateCacheProvider()
    {
        return new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class NoOpCompatibilityService : IPcCompatibilityService
    {
        public Task<PcValidateResponse> ValidateAsync(PcValidateRequest request)
        {
            return Task.FromResult(new PcValidateResponse());
        }
    }

    private sealed class FailingAutoBuilderService : IPcAutoBuilderService
    {
        public const string ExpectedMessage = "test failure";

        public Task<AutoBuildResponse> BuildAsync(AutoBuildRequest request)
        {
            throw new AutoBuildException(ExpectedMessage);
        }
    }
}
