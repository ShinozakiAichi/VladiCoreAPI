using System;
using System.Collections.Generic;

namespace VladiCore.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string Attributes { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Category Category { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
        public virtual ICollection<ProductPriceHistory> PriceHistory { get; set; }

        public Product()
        {
            OrderItems = new HashSet<OrderItem>();
            PriceHistory = new HashSet<ProductPriceHistory>();
        }
    }
}
