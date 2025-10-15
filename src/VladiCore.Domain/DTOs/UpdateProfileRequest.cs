using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class UpdateProfileRequest
{
    [MaxLength(64)]
    public string? DisplayName { get; set; }

    [Phone]
    [MaxLength(32)]
    public string? PhoneNumber { get; set; }
}
