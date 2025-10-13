using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class CreateReviewRequest
{
    private const int MaxPhotoCount = 6;

    [Range(1, 5)]
    public byte Rating { get; set; }

    [MaxLength(120)]
    public string? Title { get; set; }

    [Required]
    [MinLength(10)]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(MaxPhotoCount)]
    public List<string> Photos { get; set; } = new();

    [MaxLength(64)]
    public string? UserId { get; set; }
}
