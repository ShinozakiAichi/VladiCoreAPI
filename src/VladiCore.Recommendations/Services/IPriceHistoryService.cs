using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VladiCore.Domain.DTOs;

namespace VladiCore.Recommendations.Services
{
    public interface IPriceHistoryService
    {
        Task<IReadOnlyCollection<PricePointDto>> GetSeriesAsync(int productId, DateTime from, DateTime to, string bucket);
    }
}
