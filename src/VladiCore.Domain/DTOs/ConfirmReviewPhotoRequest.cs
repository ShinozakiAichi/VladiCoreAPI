using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class ConfirmReviewPhotoRequest
{
    [Required]
    [MaxLength(256)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ETag { get; set; }
}
