using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class TrackViewDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 3)]
    public string SessionId { get; set; } = string.Empty;

    public int? UserId { get; set; }
}
