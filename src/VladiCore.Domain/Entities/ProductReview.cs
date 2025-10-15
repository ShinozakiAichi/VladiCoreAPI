using System;

namespace VladiCore.Domain.Entities;

public class ProductReview
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public Guid UserId { get; set; }

    public byte Rating { get; set; }

    public string? Title { get; set; }

    public string Text { get; set; } = string.Empty;

    public string[] Photos { get; set; } = Array.Empty<string>();

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public string? ModerationNote { get; set; }

    public bool IsDeleted { get; set; }

    public int UsefulUp { get; set; }

    public int UsefulDown { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<ProductReviewVote> Votes { get; set; } = new List<ProductReviewVote>();
}
