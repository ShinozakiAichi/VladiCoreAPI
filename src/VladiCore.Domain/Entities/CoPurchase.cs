using System;

namespace VladiCore.Domain.Entities;

public class CoPurchase
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public int WithProductId { get; set; }

    public double Score { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
