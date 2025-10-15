using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class PresignRequest
{
    [Required]
    [RegularExpression("^(products|reviews)$", ErrorMessage = "Type must be either 'products' or 'reviews'.")]
    public string Type { get; set; } = "reviews";

    [Required]
    [MaxLength(64)]
    public string ContentType { get; set; } = string.Empty;

    [Range(1, 20_000_000)]
    public long Size { get; set; } = 5_000_000;

    [Range(1, int.MaxValue)]
    public int? EntityId { get; set; }
}
