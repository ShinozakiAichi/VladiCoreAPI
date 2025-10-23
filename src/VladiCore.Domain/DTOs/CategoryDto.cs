using System;

namespace VladiCore.Domain.DTOs;

public class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public DateTime CreatedAt { get; set; }
}
