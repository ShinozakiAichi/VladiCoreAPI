using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using VladiCore.Data.Infrastructure;
using VladiCore.Domain.DTOs;

namespace VladiCore.Recommendations.Services
{
    /// <summary>
    /// Provides "frequently bought/viewed together" recommendations.
    /// </summary>
    public class RecommendationService : IRecommendationService
    {
        private const double OrdersWeight = 0.7;
        private const double ViewsWeight = 0.3;
        private readonly IMySqlConnectionFactory _connectionFactory;

        public RecommendationService(IMySqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyCollection<RecommendationDto>> GetRecommendationsAsync(int productId, int take, int skip = 0)
        {
            using (var connection = _connectionFactory.Create())
            {
                var ordersSql = @"SELECT oi2.ProductId AS AlsoProductId, COUNT(*) AS PairCount
                                   FROM OrderItems oi1
                                   JOIN OrderItems oi2 ON oi1.OrderId = oi2.OrderId AND oi1.ProductId <> oi2.ProductId
                                   WHERE oi1.ProductId = @ProductId
                                   GROUP BY oi2.ProductId
                                   ORDER BY PairCount DESC
                                   LIMIT @Take OFFSET @Skip";

                var viewsSql = @"SELECT v2.ProductId AS AlsoProductId, COUNT(*) AS ViewPairCount
                                  FROM ProductViews v1
                                  JOIN ProductViews v2 ON v1.SessionId = v2.SessionId AND v1.ProductId <> v2.ProductId
                                  WHERE v1.ProductId = @ProductId AND v1.ViewedAt >= (UTC_TIMESTAMP(6) - INTERVAL 30 DAY)
                                  GROUP BY v2.ProductId
                                  ORDER BY ViewPairCount DESC
                                  LIMIT @Take";

                var orderPairs = (await connection.QueryAsync(ordersSql, new { ProductId = productId, Take = take, Skip = skip }))
                    .ToDictionary(r => (int)r.AlsoProductId, r => (long)r.PairCount);
                var viewPairs = (await connection.QueryAsync(viewsSql, new { ProductId = productId, Take = take }))
                    .ToDictionary(r => (int)r.AlsoProductId, r => (long)r.ViewPairCount);

                var candidates = orderPairs.Keys.Union(viewPairs.Keys).ToList();
                if (!candidates.Any())
                {
                    return Array.Empty<RecommendationDto>();
                }

                double maxOrder = orderPairs.Any() ? orderPairs.Values.Max() : 0d;
                double maxView = viewPairs.Any() ? viewPairs.Values.Max() : 0d;

                var scores = new Dictionary<int, double>();
                foreach (var candidate in candidates)
                {
                    var orderScore = maxOrder > 0 ? orderPairs.GetValueOrDefault(candidate) / maxOrder : 0d;
                    var viewScore = maxView > 0 ? viewPairs.GetValueOrDefault(candidate) / maxView : 0d;
                    scores[candidate] = Math.Round(orderScore * OrdersWeight + viewScore * ViewsWeight, 6);
                }

                var detailsSql = @"SELECT Id, Name, Price FROM Products WHERE Id IN @Ids";
                var details = await connection.QueryAsync(detailsSql, new { Ids = candidates });

                return details
                    .Select(d => new RecommendationDto
                    {
                        ProductId = d.Id,
                        Name = d.Name,
                        Price = d.Price,
                        Score = scores.GetValueOrDefault((int)d.Id)
                    })
                    .OrderByDescending(r => r.Score)
                    .ThenBy(r => r.ProductId)
                    .Take(take)
                    .ToList();
            }
        }
    }
}
