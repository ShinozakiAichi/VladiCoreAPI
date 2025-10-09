using System;
using System.Collections.Generic;

namespace VladiCore.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual ICollection<OrderItem> Items { get; set; }

        public Order()
        {
            Items = new HashSet<OrderItem>();
        }
    }
}
