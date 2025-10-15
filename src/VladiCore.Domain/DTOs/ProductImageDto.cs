using System;

namespace VladiCore.Domain.DTOs;

public class ProductImageDto
{
    public long Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ETag { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
