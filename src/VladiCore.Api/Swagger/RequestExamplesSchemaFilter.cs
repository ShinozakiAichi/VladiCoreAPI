using System;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Swagger;

public class RequestExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema == null)
        {
            return;
        }

        if (context.Type == typeof(PcValidateRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["cpuId"] = new OpenApiInteger(1),
                ["motherboardId"] = new OpenApiInteger(12),
                ["gpuId"] = new OpenApiInteger(5),
                ["ramId"] = new OpenApiInteger(21),
                ["storageIds"] = new OpenApiArray { new OpenApiInteger(31) },
                ["psuId"] = new OpenApiInteger(18),
                ["caseId"] = new OpenApiInteger(7)
            };
        }
        else if (context.Type == typeof(AutoBuildRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["budget"] = new OpenApiDouble(1200),
                ["priorities"] = new OpenApiArray
                {
                    new OpenApiString("gaming"),
                    new OpenApiString("streaming")
                },
                ["platform"] = new OpenApiString("amd")
            };
        }
        else if (context.Type == typeof(ProductDto))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiInteger(42),
                ["sku"] = new OpenApiString("CPU-5600X"),
                ["name"] = new OpenApiString("Ryzen 5 5600X"),
                ["categoryId"] = new OpenApiInteger(3),
                ["price"] = new OpenApiDouble(239.99),
                ["oldPrice"] = new OpenApiDouble(259.99),
                ["attributes"] = new OpenApiString("{\"socket\":\"AM4\",\"cores\":6}"),
                ["images"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["key"] = new OpenApiString("products/42/main.jpg"),
                        ["url"] = new OpenApiString("https://cdn.example.com/products/42/main.jpg"),
                        ["thumbnailUrl"] = new OpenApiString("https://cdn.example.com/products/42/main_thumb.jpg"),
                        ["createdAt"] = new OpenApiString(DateTime.UtcNow.ToString("O"))
                    }
                }
            };
        }
        else if (context.Type == typeof(TrackViewDto))
        {
            schema.Example = new OpenApiObject
            {
                ["productId"] = new OpenApiInteger(42),
                ["sessionId"] = new OpenApiString("sess-1234"),
                ["userId"] = new OpenApiString("user-5678")
            };
        }
        else if (context.Type == typeof(CreateReviewRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["rating"] = new OpenApiInteger(5),
                ["title"] = new OpenApiString("Отличный процессор"),
                ["body"] = new OpenApiString("Собрал ПК, температура стабильная, рекомендую."),
                ["photos"] = new OpenApiArray
                {
                    new OpenApiString("https://cdn.example.com/reviews/img1.jpg")
                }
            };
        }
        else if (context.Type == typeof(PresignRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["extension"] = new OpenApiString("jpg"),
                ["contentType"] = new OpenApiString("image/jpeg"),
                ["maxFileSize"] = new OpenApiInteger(5000000),
                ["purpose"] = new OpenApiString("reviews")
            };
        }
    }
}
