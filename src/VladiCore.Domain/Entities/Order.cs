using VladiCore.Domain.Enums;

namespace VladiCore.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CartId { get; set; }
    public OrderStatus Status { get; set; }
    public Currency Currency { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public Guid? ShippingAddressId { get; set; }
    public DateTime? PlacedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public Cart? Cart { get; set; }
    public UserAddress? ShippingAddress { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
