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
        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), Name = "admin" },
                new Role { Id = Guid.NewGuid(), Name = "user" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "admin");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "allenoveartem@gmail.com",
                PasswordHash = hasher.Hash("Admin123!"),
                RoleId = adminRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "default",
                Slug = "default",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Categories.Add(category);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Name = "demo-product",
                Slug = "demo-product",
                Description = "Demo product",
                BasePrice = 10m,
                Currency = Currency.USD,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Products.Add(product);

            db.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ImageUrl = "https://example.com/image.jpg",
                AltText = "Demo image",
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            });

            db.Stocks.Add(new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 100,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }
}
