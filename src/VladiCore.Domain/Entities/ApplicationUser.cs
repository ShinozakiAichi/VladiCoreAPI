using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace VladiCore.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductReview> Reviews { get; set; } = new HashSet<ProductReview>();
}
