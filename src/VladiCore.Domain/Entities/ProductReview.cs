using System;

namespace VladiCore.Domain.Entities;

public class ProductReview
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public string? UserId { get; set; }

    public byte Rating { get; set; }

    public string? Title { get; set; }

    public string Body { get; set; } = string.Empty;

    public string[] Photos { get; set; } = Array.Empty<string>();

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
