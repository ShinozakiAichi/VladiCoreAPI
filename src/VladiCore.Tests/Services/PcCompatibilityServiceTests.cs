using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.PcBuilder.Services;
using VladiCore.Tests.Infrastructure;

namespace VladiCore.Tests.Services;

[TestFixture]
public class PcCompatibilityServiceTests
{
    [Test]
    public async Task should_detect_socket_mismatch()
    {
        await using var context = CreateContext();
        var service = new PcCompatibilityService(context);
        var request = new PcValidateRequest
        {
            CpuId = 1,
            MotherboardId = 2,
            RamId = 1,
            GpuId = 1,
            PsuId = 1,
            CaseId = 1,
            StorageIds = new List<int> { 1 }
        };

        var result = await service.ValidateAsync(request);
        result.IsCompatible.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "CPU_SOCKET_MISMATCH");
    }

    [Test]
    public async Task should_pass_for_known_good_configuration()
    {
        await using var context = CreateContext();
        var service = new PcCompatibilityService(context);
        var request = new PcValidateRequest
        {
            CpuId = 2,
            MotherboardId = 1,
            RamId = 2,
            GpuId = 1,
            PsuId = 2,
            CaseId = 2,
            CoolerId = 1,
            StorageIds = new List<int> { 2, 3 }
        };

        var result = await service.ValidateAsync(request);
        result.IsCompatible.Should().BeTrue("all chosen parts are compatible in seed data");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(TestConfiguration.ConnectionString, ServerVersion.AutoDetect(TestConfiguration.ConnectionString))
            .Options;
        return new AppDbContext(options);
    }
}
