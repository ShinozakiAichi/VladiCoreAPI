namespace VladiCore.Domain.Entities;

public class CartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }

    public Cart? Cart { get; set; }
    public Product? Product { get; set; }
    public ProductVariant? Variant { get; set; }
}
