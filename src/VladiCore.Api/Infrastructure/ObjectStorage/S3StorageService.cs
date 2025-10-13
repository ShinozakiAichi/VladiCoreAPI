using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Infrastructure.ObjectStorage;

public class S3StorageService : IObjectStorageService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(10);
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3StorageService> _logger;
    private readonly S3Options _options;

    public S3StorageService(IAmazonS3 client, IOptions<S3Options> options, ILogger<S3StorageService> logger)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            throw new InvalidOperationException("S3 bucket name must be configured");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("S3 endpoint must be configured");
        }
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        try
        {
            await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload object {Key} to bucket {Bucket}", key, _options.Bucket);
            throw;
        }

        return BuildUrl(key);
    }

    public PresignResponse CreatePresignedUpload(string key, string contentType, TimeSpan expiresIn, long maxFileSize)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        var expiry = expiresIn <= TimeSpan.Zero ? DefaultExpiry : expiresIn;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType
        };

        var url = _client.GetPreSignedURL(request);

        return new PresignResponse
        {
            Key = key,
            Url = url,
            Fields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Content-Type"] = contentType,
                ["x-amz-meta-max-length"] = maxFileSize.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    public string BuildUrl(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must be provided", nameof(key));
        }

        if (!string.IsNullOrWhiteSpace(_options.CdnBaseUrl))
        {
            return $"{_options.CdnBaseUrl!.TrimEnd('/')}/{key}";
        }

        if (Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return $"{endpoint.Scheme}://{endpoint.Authority}/{_options.Bucket}/{key}";
        }

        var endpointHost = _options.Endpoint.TrimEnd('/');
        var scheme = _options.UseSsl ? "https" : "http";
        return $"{scheme}://{endpointHost}/{_options.Bucket}/{key}";
    }
}
