using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using VladiCore.Data.Infrastructure;
using VladiCore.Domain.DTOs;

namespace VladiCore.Recommendations.Services
{
    /// <summary>
    /// Aggregates price history using optimized SQL buckets.
    /// </summary>
    public class PriceHistoryService : IPriceHistoryService
    {
        private readonly IMySqlConnectionFactory _connectionFactory;

        public PriceHistoryService(IMySqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyCollection<PricePointDto>> GetSeriesAsync(int productId, DateTime from, DateTime to, string bucket)
        {
            if (to <= from)
            {
                throw new ArgumentException("The 'to' date must be greater than 'from'.");
            }

            var sql = BuildSql(bucket);
            using (var connection = _connectionFactory.Create())
            {
                var rows = await connection.QueryAsync(sql, new { ProductId = productId, From = from, To = to });
                var points = new List<PricePointDto>();
                foreach (dynamic row in rows)
                {
                    points.Add(new PricePointDto
                    {
                        Date = Convert.ToString(row.Bucket, CultureInfo.InvariantCulture),
                        AvgPrice = row.AvgPrice
                    });
                }

                return points;
            }
        }

        private static string BuildSql(string bucket)
        {
            switch (bucket)
            {
                case "day":
                    return @"SELECT DATE(ChangedAt) AS Bucket, AVG(Price) AS AvgPrice
                             FROM ProductPriceHistory
                             WHERE ProductId = @ProductId AND ChangedAt BETWEEN @From AND @To
                             GROUP BY DATE(ChangedAt)
                             ORDER BY Bucket";
                case "week":
                    return @"SELECT YEARWEEK(ChangedAt, 3) AS Bucket, AVG(Price) AS AvgPrice
                             FROM ProductPriceHistory
                             WHERE ProductId = @ProductId AND ChangedAt BETWEEN @From AND @To
                             GROUP BY YEARWEEK(ChangedAt, 3)
                             ORDER BY Bucket";
                case "month":
                    return @"SELECT DATE_FORMAT(ChangedAt, '%Y-%m-01') AS Bucket, AVG(Price) AS AvgPrice
                             FROM ProductPriceHistory
                             WHERE ProductId = @ProductId AND ChangedAt BETWEEN @From AND @To
                             GROUP BY DATE_FORMAT(ChangedAt, '%Y-%m-01')
                             ORDER BY Bucket";
                default:
                    throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported bucket");
            }
        }
    }
}
