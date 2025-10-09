using System.Threading.Tasks;
using VladiCore.Domain.DTOs;

namespace VladiCore.PcBuilder.Services
{
    public interface IPcAutoBuilderService
    {
        Task<AutoBuildResponse> BuildAsync(AutoBuildRequest request);
    }
}
