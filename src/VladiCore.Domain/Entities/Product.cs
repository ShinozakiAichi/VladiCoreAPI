using System;
using System.Collections.Generic;

namespace VladiCore.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public required string Sku { get; set; }

    public required string Name { get; set; }

    public int CategoryId { get; set; }

    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public string? Attributes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Category Category { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = new HashSet<OrderItem>();

    public ICollection<ProductPriceHistory> PriceHistory { get; set; } = new HashSet<ProductPriceHistory>();

    public ICollection<ProductReview> Reviews { get; set; } = new HashSet<ProductReview>();

    public ICollection<ProductImage> Images { get; set; } = new HashSet<ProductImage>();
}
