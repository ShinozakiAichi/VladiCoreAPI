using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VladiCore.Api.Infrastructure.ObjectStorage;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("storage")]
[Authorize]
public class StorageController : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypeExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly AppDbContext _dbContext;
    private readonly IObjectStorageService _storageService;

    public StorageController(AppDbContext dbContext, IObjectStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    [HttpPost("presign")]
    public async Task<ActionResult<PresignResponse>> CreatePresign(
        [FromBody] PresignRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!ContentTypeExtensions.ContainsKey(request.ContentType))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported content type",
                Detail = "Allowed: image/jpeg, image/png, image/webp",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Size > 10_000_000)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "File too large",
                Detail = "Maximum allowed size is 10 MB",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.EntityId is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "EntityId is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        string prefix;
        switch (request.Type)
        {
            case "products":
                if (!await _dbContext.Products.AnyAsync(p => p.Id == request.EntityId.Value, cancellationToken).ConfigureAwait(false))
                {
                    return NotFound(new ProblemDetails { Title = "Product not found", Status = StatusCodes.Status404NotFound });
                }

                prefix = $"products/{request.EntityId.Value}";
                break;
            case "reviews":
                if (!await _dbContext.ProductReviews.AnyAsync(r => r.Id == request.EntityId.Value, cancellationToken).ConfigureAwait(false))
                {
                    return NotFound(new ProblemDetails { Title = "Review not found", Status = StatusCodes.Status404NotFound });
                }

                prefix = $"reviews/{request.EntityId.Value}";
                break;
            default:
                return BadRequest(new ProblemDetails { Title = "Unsupported type", Status = StatusCodes.Status400BadRequest });
        }

        var key = $"{prefix}/{Guid.NewGuid():N}{ContentTypeExtensions[request.ContentType]}";
        var presign = _storageService.CreatePresignedUpload(key, request.ContentType, TimeSpan.FromMinutes(10), request.Size);
        return Ok(presign);
    }
}
