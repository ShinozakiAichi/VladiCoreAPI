using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VladiCore.Api.Infrastructure.ObjectStorage;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { "jpg", "jpeg", "png", "webp" };
    private readonly ReviewOptions _reviewOptions;
    private readonly IObjectStorageService _storage;

    public UploadsController(IObjectStorageService storage, IOptions<ReviewOptions> reviewOptions)
    {
        _storage = storage;
        _reviewOptions = reviewOptions.Value;
    }

    [HttpPost("presign")]
    [AllowAnonymous]
    public IActionResult Presign([FromBody] PresignRequest request)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        if (_reviewOptions.RequireAuthentication && !(User?.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        var extension = request.Extension.Trim().TrimStart('.').ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, extension) < 0)
        {
            return BadRequest("Unsupported file extension.");
        }

        var contentType = request.ContentType.Trim();
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only image uploads are supported.");
        }

        var prefix = string.Equals(request.Purpose, "reviews", StringComparison.OrdinalIgnoreCase)
            ? "reviews"
            : "uploads";
        var key = $"{prefix}/{Guid.NewGuid():N}.{extension}";

        var presigned = _storage.CreatePresignedUpload(key, contentType, TimeSpan.FromMinutes(10), request.MaxFileSize);

        return Ok(presigned);
    }
}
