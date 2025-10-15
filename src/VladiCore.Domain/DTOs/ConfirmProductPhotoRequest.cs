using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class ConfirmProductPhotoRequest
{
    [Required]
    [MaxLength(256)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ETag { get; set; } = string.Empty;

    [Range(0, 100)]
    public int SortOrder { get; set; }
}
