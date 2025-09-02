namespace VladiCore.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
