using VladiCore.Domain.Enums;

namespace VladiCore.Domain.Entities;

public class Cart
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string SessionKey { get; set; } = string.Empty;
    public Currency Currency { get; set; }
    public bool IsCheckedOut { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
