using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using VladiCore.Domain.Entities;

namespace VladiCore.Data.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductReview> Reviews { get; set; } = new HashSet<ProductReview>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new HashSet<RefreshToken>();
}
