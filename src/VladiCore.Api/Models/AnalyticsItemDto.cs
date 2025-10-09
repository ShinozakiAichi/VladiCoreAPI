namespace VladiCore.Api.Models;

public class AnalyticsItemDto
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public long Count { get; set; }
}
