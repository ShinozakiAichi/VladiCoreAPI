namespace VladiCore.Api.Infrastructure.Options;

public class S3Options
{
    public string Endpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    public string? CdnBaseUrl { get; set; }
}
