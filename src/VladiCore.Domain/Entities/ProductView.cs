using System;

namespace VladiCore.Domain.Entities
{
    public class ProductView
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? UserId { get; set; }
        public string SessionId { get; set; }
        public DateTime ViewedAt { get; set; }

        public virtual Product Product { get; set; }
    }
}
