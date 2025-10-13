using System.Collections.Generic;

namespace VladiCore.Domain.DTOs;

public class PresignResponse
{
    public string Url { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public IDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
}
