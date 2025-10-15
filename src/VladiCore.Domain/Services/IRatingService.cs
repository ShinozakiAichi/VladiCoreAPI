using System.Threading;
using System.Threading.Tasks;

namespace VladiCore.Domain.Services;

public interface IRatingService
{
    Task RecomputeAsync(int productId, CancellationToken cancellationToken = default);
}
