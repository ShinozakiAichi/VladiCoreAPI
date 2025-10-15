using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class CreateReviewRequest
{
    public const int MaxPhotoCount = 8;

    public const int MinTextLength = 5;

    public const int MaxTextLength = 5000;

    [Range(1, 5)]
    public byte Rating { get; set; }

    [MaxLength(140)]
    public string? Title { get; set; }

    [Required]
    [MinLength(MinTextLength)]
    [MaxLength(MaxTextLength)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(MaxPhotoCount)]
    public List<string> Photos { get; set; } = new();
}
