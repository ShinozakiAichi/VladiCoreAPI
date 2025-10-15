using System;
using System.Collections.Generic;

namespace VladiCore.Domain.Entities;

public class Order
{
    public int Id { get; set; }

    public Guid? UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new HashSet<OrderItem>();
}
