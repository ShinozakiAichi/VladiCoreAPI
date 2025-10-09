namespace VladiCore.Domain.DTOs
{
    public class RecommendationDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public double Score { get; set; }
    }
}
