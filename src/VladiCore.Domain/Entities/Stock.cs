namespace VladiCore.Domain.Entities;

public class Stock
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Product? Product { get; set; }
    public ProductVariant? Variant { get; set; }
}
