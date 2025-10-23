using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using VladiCore.Domain.Serialization;

namespace VladiCore.Domain.DTOs;

public class TrackViewDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 3)]
    public string SessionId { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? UserId { get; set; }
}
