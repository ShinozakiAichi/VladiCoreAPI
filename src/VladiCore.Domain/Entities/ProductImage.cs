using System;

namespace VladiCore.Domain.Entities;

public class ProductImage
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string? ThumbnailKey { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
