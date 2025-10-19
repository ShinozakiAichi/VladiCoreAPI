using System;
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
using VladiCore.PcBuilder.Services;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class PcControllerTests
{
    [Test]
    public async Task AutoBuild_ShouldReturnProblemDetailsWhenNoBuildIsPossible()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var cache = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
        var controller = new PcController(
            context,
            cache,
            new SlidingWindowRateLimiter(),
            new StubCompatibilityService(),
            new ThrowingAutoBuilderService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new AutoBuildRequest
        {
            Budget = 1200,
            Priorities = new List<string> { "gaming" }
        };

        var result = await controller.AutoBuild(request);

        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        objectResult.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Contain("Cannot build");
    }

    private sealed class StubCompatibilityService : IPcCompatibilityService
    {
        public Task<PcValidateResponse> ValidateAsync(PcValidateRequest request)
        {
            return Task.FromResult(new PcValidateResponse { IsCompatible = true });
        }
    }

    private sealed class ThrowingAutoBuilderService : IPcAutoBuilderService
    {
        public Task<AutoBuildResponse> BuildAsync(AutoBuildRequest request)
        {
            throw new InvalidOperationException("Cannot build a compatible PC within the provided budget.");
        }
    }
}
