using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class RejectReviewRequest
{
    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Note { get; set; }
}
