using Microsoft.EntityFrameworkCore;
using VladiCore.App.Exceptions;
using VladiCore.Data;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.App.Services;

public sealed class StaffService : IStaffService
{
    private readonly AppDbContext _db;

    public StaffService(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<Branch>> GetBranchesAsync() => _db.Branches.AsNoTracking().ToListAsync();

    public async Task<Branch> CreateBranchAsync(string name, string? address, string? phone)
    {
        var branch = new Branch { Name = name, Address = address, Phone = phone };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        return branch;
    }

    public async Task<Employee> CreateEmployeeAsync(Guid userId, UserRole position, long? branchId)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId))
            throw new AppException(404, "user_not_found", "User not found");

        var employee = new Employee { Id = Guid.NewGuid(), UserId = userId, BranchId = branchId, Position = position, IsActive = true };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }
}
