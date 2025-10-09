using System.Collections.Generic;
using System.Threading.Tasks;
using VladiCore.Domain.DTOs;

namespace VladiCore.Recommendations.Services
{
    public interface IRecommendationService
    {
        Task<IReadOnlyCollection<RecommendationDto>> GetRecommendationsAsync(int productId, int take, int skip = 0);
    }
}
