using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class UpsertProductRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int CategoryId { get; set; }

    [Range(typeof(decimal), "0", "9999999")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    public Dictionary<string, object>? Specs { get; set; }

    [MaxLength(8000)]
    public string? Description { get; set; }
}
