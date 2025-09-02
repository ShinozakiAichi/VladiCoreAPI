namespace VladiCore.Api.Models.Catalog;

public class CreateProductRequest
{
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
