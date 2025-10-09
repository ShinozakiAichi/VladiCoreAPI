using VladiCore.Data.Contexts;
using VladiCore.Data.Infrastructure;
using VladiCore.PcBuilder.Services;
using VladiCore.Recommendations.Services;

namespace VladiCore.Api.Infrastructure
{
    /// <summary>
    /// Minimal service locator to wire dependencies without external DI.
    /// </summary>
    public static class ServiceContainer
    {
        public static ICacheProvider Cache { get; } = new MemoryCacheProvider();
        public static IRateLimiter RateLimiter { get; } = new SlidingWindowRateLimiter();
        public static IMySqlConnectionFactory ConnectionFactory { get; } = new MySqlConnectionFactory();

        public static VladiCoreContext CreateContext()
        {
            return new VladiCoreContext();
        }

        public static IPriceHistoryService CreatePriceHistoryService()
        {
            return new PriceHistoryService(ConnectionFactory);
        }

        public static IRecommendationService CreateRecommendationService()
        {
            return new RecommendationService(ConnectionFactory);
        }

        public static IPcCompatibilityService CreateCompatibilityService(VladiCoreContext context)
        {
            return new PcCompatibilityService(context);
        }

        public static IPcAutoBuilderService CreateAutoBuilderService(VladiCoreContext context)
        {
            return new PcAutoBuilderService(context, CreateCompatibilityService(context), CreatePriceHistoryService());
        }
    }
}
