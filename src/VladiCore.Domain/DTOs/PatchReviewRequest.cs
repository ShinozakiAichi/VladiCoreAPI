using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Domain.DTOs;

public class PatchReviewRequest : IValidatableObject
{
    [Range(1, 5)]
    public byte? Rating { get; set; }

    [MaxLength(140)]
    public string? Title { get; set; }

    [MinLength(CreateReviewRequest.MinTextLength)]
    [MaxLength(CreateReviewRequest.MaxTextLength)]
    public string? Text { get; set; }

    [MaxLength(CreateReviewRequest.MaxPhotoCount)]
    public List<string>? Photos { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Rating == null && Title == null && Text == null && Photos == null)
        {
            yield return new ValidationResult("At least one field must be provided.");
        }
    }
}
