using System.Threading.Tasks;
using VladiCore.Domain.DTOs;

namespace VladiCore.PcBuilder.Services
{
    public interface IPcCompatibilityService
    {
        Task<PcValidateResponse> ValidateAsync(PcValidateRequest request);
    }
}
