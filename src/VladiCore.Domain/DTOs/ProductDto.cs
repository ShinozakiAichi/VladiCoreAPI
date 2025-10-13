using System;
using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class ProductDto
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public string? Attributes { get; set; }

    public IReadOnlyList<ProductImageDto> Images { get; set; } = Array.Empty<ProductImageDto>();
}
