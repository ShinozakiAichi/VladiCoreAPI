using System;

namespace VladiCore.Domain.Entities
{
    public class ProductPriceHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public DateTime ChangedAt { get; set; }

        public virtual Product Product { get; set; }
    }
}
