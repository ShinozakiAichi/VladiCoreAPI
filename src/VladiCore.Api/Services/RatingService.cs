using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VladiCore.Data.Contexts;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Services;

namespace VladiCore.Api.Services;

public class RatingService : IRatingService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RatingService> _logger;

    public RatingService(AppDbContext dbContext, ILogger<RatingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecomputeAsync(int productId, CancellationToken cancellationToken = default)
    {
        var approvedReviews = await _dbContext.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var average = approvedReviews.Count == 0 ? 0m : Math.Round((decimal)approvedReviews.Average(r => r), 2);
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            .ConfigureAwait(false);
        if (product == null)
        {
            _logger.LogWarning("Attempted to recompute rating for missing product {ProductId}", productId);
            return;
        }

        product.AverageRating = average;
        product.RatingsCount = approvedReviews.Count;
        product.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
