using System;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class RefreshRequest
{
    [Required]
    public Guid RefreshToken { get; set; }
}
