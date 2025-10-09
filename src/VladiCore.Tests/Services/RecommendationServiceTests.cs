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
    public class RecommendationServiceTests
    {
        [Test]
        public async Task should_rank_products_by_combined_scores()
        {
            var factory = new MySqlConnectionFactory(TestConfiguration.ConnectionString);
            var service = new RecommendationService(factory);
            var recommendations = await service.GetRecommendationsAsync(1, 5);

            recommendations.Should().NotBeEmpty();
            recommendations[0].Score.Should().BeGreaterThan(0);
        }
    }
}
