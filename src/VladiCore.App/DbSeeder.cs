using Microsoft.EntityFrameworkCore;
using VladiCore.App.Services;
using VladiCore.Data;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Enums;

namespace VladiCore.App;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@example.com",
                FullName = "Admin",
                PasswordHash = hasher.Hash("Admin123!"),
                Role = UserRole.Admin
            };
            db.Users.Add(admin);

            var branch1 = new Branch { Name = "Main Store" };
            var branch2 = new Branch { Name = "Outlet" };
            db.Branches.AddRange(branch1, branch2);

            db.Employees.Add(new Employee
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                BranchId = branch1.Id,
                Position = UserRole.Admin,
                IsActive = true
            });

            await db.SaveChangesAsync();
        }
    }
}
