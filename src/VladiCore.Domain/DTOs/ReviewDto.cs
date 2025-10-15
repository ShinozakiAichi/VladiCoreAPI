using System;
using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class ReviewDto
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public Guid UserId { get; set; }

    public string? UserDisplay { get; set; }

    public byte Rating { get; set; }

    public string? Title { get; set; }

    public string Text { get; set; } = string.Empty;

    public IReadOnlyList<string> Photos { get; set; } = Array.Empty<string>();

    public string Status { get; set; } = string.Empty;

    public string? ModerationNote { get; set; }

    public int UsefulUp { get; set; }

    public int UsefulDown { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
