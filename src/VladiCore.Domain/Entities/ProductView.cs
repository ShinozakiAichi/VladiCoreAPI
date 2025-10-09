using System;

namespace VladiCore.Domain.Entities;

public class ProductView
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? UserId { get; set; }

    public required string SessionId { get; set; }

    public DateTime ViewedAt { get; set; }

    public Product Product { get; set; } = null!;
}
