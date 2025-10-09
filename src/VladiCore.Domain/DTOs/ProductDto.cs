namespace VladiCore.Domain.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string Attributes { get; set; }
    }
}
