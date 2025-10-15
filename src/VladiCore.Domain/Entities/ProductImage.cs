using System;

namespace VladiCore.Domain.Entities;

public class ProductImage
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string ETag { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
