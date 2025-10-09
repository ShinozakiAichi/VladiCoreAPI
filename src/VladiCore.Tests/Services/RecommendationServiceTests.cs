using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using VladiCore.Data.Infrastructure;
using VladiCore.Recommendations.Services;
using VladiCore.Tests.Infrastructure;

namespace VladiCore.Tests.Services;

[TestFixture]
public class RecommendationServiceTests
{
    [Test]
    public async Task should_rank_products_by_combined_scores()
    {
        var service = new RecommendationService(CreateFactory());
        var recommendations = await service.GetRecommendationsAsync(1, 5);

        recommendations.Should().NotBeEmpty();
        recommendations[0].Score.Should().BeGreaterThan(0);
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
