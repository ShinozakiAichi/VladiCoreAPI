using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using VladiCore.Data.Infrastructure;
using VladiCore.Recommendations.Services;
using VladiCore.Tests.Infrastructure;

namespace VladiCore.Tests.Services
{
    [TestFixture]
    public class PriceHistoryServiceTests
    {
        [Test]
        public async Task should_bucket_prices_by_day()
        {
            var factory = new MySqlConnectionFactory(TestConfiguration.ConnectionString);
            var service = new PriceHistoryService(factory);
            var from = DateTime.UtcNow.AddDays(-10);
            var to = DateTime.UtcNow;

            var series = await service.GetSeriesAsync(1, from, to, "day");

            series.Should().NotBeEmpty();
            series.Select(p => p.Date).Distinct().Count().Should().Be(series.Count);
        }
    }
}
