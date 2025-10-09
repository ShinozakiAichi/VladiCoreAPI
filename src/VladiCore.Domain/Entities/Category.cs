using System;
using System.Collections.Generic;

namespace VladiCore.Domain.Entities
{
    /// <summary>
    /// Represents a product category in the catalog hierarchy.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Product> Products { get; set; }

        public Category()
        {
            Products = new HashSet<Product>();
        }
    }
}
