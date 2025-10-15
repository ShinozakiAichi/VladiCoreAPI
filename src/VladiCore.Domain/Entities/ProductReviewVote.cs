using System;

namespace VladiCore.Domain.Entities;

public class ProductReviewVote
{
    public long ReviewId { get; set; }

    public Guid UserId { get; set; }

    public sbyte Value { get; set; }

    public ProductReview? Review { get; set; }
}
