using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;
using VladiCore.PcBuilder.Services;
using VladiCore.Recommendations.Services;

namespace VladiCore.Tests.PcBuilder;

[TestFixture]
public class PcAutoBuilderServiceTests
{
    [Test]
    public async Task BuildAsync_ShouldReturnResponse_WhenPriceHistoryServiceFails()
    {
        await using var context = CreateContext();
        SeedMinimalCatalog(context);

        var service = new PcAutoBuilderService(
            context,
            new AlwaysCompatibleService(),
            new ThrowingPriceHistoryService(),
            NullLogger<PcAutoBuilderService>.Instance);

        var request = new AutoBuildRequest
        {
            Budget = 2000,
            Platform = "amd",
            Priorities = new List<string> { "gaming" }
        };

        var response = await service.BuildAsync(request);

        response.PriceCharts.Should().BeEmpty();
        response.Rationale.Should().Contain("Price history data is temporarily unavailable.");
        response.Parts.Should().ContainKey("cpu");
        response.Total.Should().BeGreaterThan(0);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedMinimalCatalog(AppDbContext context)
    {
        context.Cpus.Add(new Cpu { Id = 1, Name = "Ryzen 5 7600", Socket = "AM5", Tdp = 105, PerfScore = 600 });
        context.Motherboards.Add(new Motherboard
        {
            Id = 1,
            Name = "MSI B650 Tomahawk",
            Socket = "AM5",
            RamType = "DDR5",
            RamMaxFreq = 6000,
            M2Slots = 2,
            PcieSlots = 2,
            FormFactor = "ATX"
        });
        context.Rams.Add(new Ram
        {
            Id = 1,
            Name = "G.Skill Trident Z5",
            Type = "DDR5",
            Freq = 5600,
            CapacityPerStick = 16,
            Sticks = 2,
            PerfScore = 250
        });
        context.Gpus.Add(new Gpu
        {
            Id = 1,
            Name = "Radeon RX 7800 XT",
            LengthMm = 300,
            Slots = 2,
            Tdp = 263,
            PerfScore = 650
        });
        context.Psus.Add(new Psu { Id = 1, Name = "Corsair RM850x", Wattage = 850, FormFactor = "ATX" });
        context.Cases.Add(new Domain.Entities.Case
        {
            Id = 1,
            Name = "Fractal North",
            GpuMaxLengthMm = 355,
            CoolerMaxHeightMm = 170,
            PsuFormFactor = "ATX"
        });
        context.Storages.Add(new Storage
        {
            Id = 1,
            Name = "Samsung 980 Pro",
            Type = StorageType.Nvme,
            CapacityGb = 1000,
            PerfScore = 320
        });

        context.Products.AddRange(
            new Product
            {
                Id = 1,
                Sku = "CPU-7600",
                Name = "Ryzen 5 7600",
                CategoryId = 1,
                Price = 250m,
                Stock = 10,
                Attributes = "{\"componentType\":\"cpu\",\"componentId\":1}"
            },
            new Product
            {
                Id = 2,
                Sku = "MB-B650",
                Name = "MSI B650 Tomahawk",
                CategoryId = 2,
                Price = 200m,
                Stock = 10,
                Attributes = "{\"componentType\":\"motherboard\",\"componentId\":1}"
            },
            new Product
            {
                Id = 3,
                Sku = "RAM-32",
                Name = "G.Skill Trident Z5",
                CategoryId = 3,
                Price = 180m,
                Stock = 10,
                Attributes = "{\"componentType\":\"ram\",\"componentId\":1}"
            },
            new Product
            {
                Id = 4,
                Sku = "GPU-7800XT",
                Name = "Radeon RX 7800 XT",
                CategoryId = 4,
                Price = 550m,
                Stock = 10,
                Attributes = "{\"componentType\":\"gpu\",\"componentId\":1}"
            },
            new Product
            {
                Id = 5,
                Sku = "PSU-850",
                Name = "Corsair RM850x",
                CategoryId = 5,
                Price = 130m,
                Stock = 10,
                Attributes = "{\"componentType\":\"psu\",\"componentId\":1}"
            },
            new Product
            {
                Id = 6,
                Sku = "CASE-NORTH",
                Name = "Fractal North",
                CategoryId = 6,
                Price = 120m,
                Stock = 10,
                Attributes = "{\"componentType\":\"case\",\"componentId\":1}"
            },
            new Product
            {
                Id = 7,
                Sku = "SSD-980",
                Name = "Samsung 980 Pro",
                CategoryId = 7,
                Price = 100m,
                Stock = 10,
                Attributes = "{\"componentType\":\"storage\",\"componentId\":1}"
            });

        context.SaveChanges();
    }

    private sealed class AlwaysCompatibleService : IPcCompatibilityService
    {
        public Task<PcValidateResponse> ValidateAsync(PcValidateRequest request)
        {
            return Task.FromResult(new PcValidateResponse { IsCompatible = true });
        }
    }

    private sealed class ThrowingPriceHistoryService : IPriceHistoryService
    {
        public Task<IReadOnlyCollection<PricePointDto>> GetSeriesAsync(int productId, DateTime from, DateTime to, string bucket)
        {
            throw new InvalidOperationException("Simulated price history failure.");
        }
    }
}
