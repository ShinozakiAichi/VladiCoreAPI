using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class PresignRequest
{
    [Required]
    [MaxLength(16)]
    public string Extension { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string ContentType { get; set; } = string.Empty;

    [Range(1, 20_000_000)]
    public long MaxFileSize { get; set; } = 5_000_000;

    [MaxLength(64)]
    public string Purpose { get; set; } = "reviews";
}
