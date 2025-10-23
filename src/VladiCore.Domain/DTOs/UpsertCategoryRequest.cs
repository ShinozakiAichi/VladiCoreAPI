using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class UpsertCategoryRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? ParentId { get; set; }
}
