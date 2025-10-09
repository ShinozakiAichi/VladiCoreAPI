using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class AutoBuildRequest
{
    [Range(1, int.MaxValue)]
    public int Budget { get; set; }

    [Required]
    public IList<string> Priorities { get; set; } = new List<string>();

    public string? Platform { get; set; }
}
