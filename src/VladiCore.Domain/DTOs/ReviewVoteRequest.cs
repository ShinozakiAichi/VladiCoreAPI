using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class ReviewVoteRequest : IValidatableObject
{
    [Required]
    public string Value { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(Value, "up", System.StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Value, "down", System.StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("Value must be either 'up' or 'down'.", new[] { nameof(Value) });
        }
    }
}
