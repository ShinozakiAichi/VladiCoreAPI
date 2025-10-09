using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using VladiCore.Data.Infrastructure;
using VladiCore.Recommendations.Services;
using VladiCore.Tests.Infrastructure;

namespace VladiCore.Tests.Services;

[TestFixture]
public class PriceHistoryServiceTests
{
    [Test]
    public async Task should_bucket_prices_by_day()
    {
        var service = new PriceHistoryService(CreateFactory());
        var from = DateTime.UtcNow.AddDays(-10);
        var to = DateTime.UtcNow;

        var series = await service.GetSeriesAsync(1, from, to, "day");

        series.Should().NotBeEmpty();
        series.Select(p => p.Date).Distinct().Count().Should().Be(series.Count);
    }

    private static IMySqlConnectionFactory CreateFactory()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestConfiguration.ConnectionString
            })
            .Build();
        return new MySqlConnectionFactory(configuration);
    }
}
