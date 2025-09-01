using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.App.Services;

public interface IStaffService
{
    Task<List<Branch>> GetBranchesAsync();
    Task<Branch> CreateBranchAsync(string name, string? address, string? phone);
    Task<Employee> CreateEmployeeAsync(Guid userId, UserRole position, long? branchId);
}
