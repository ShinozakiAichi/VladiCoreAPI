using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Infrastructure.ObjectStorage;

public interface IObjectStorageService
{
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    PresignResponse CreatePresignedUpload(string key, string contentType, TimeSpan expiresIn, long maxFileSize);

    string BuildUrl(string key);
}
