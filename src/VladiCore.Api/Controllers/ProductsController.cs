using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Recommendations.Services;

namespace VladiCore.Api.Controllers
{
    [RoutePrefix("api/products")]
    public class ProductsController : BaseApiController
    {
        [HttpGet, Route("")]
        public HttpResponseMessage GetProducts(int? categoryId = null, string q = null, string sort = "price", int page = 1, int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(1, Math.Min(100, pageSize));

            var cacheKey = $"products:{categoryId}:{q}:{sort}:{page}:{pageSize}";
            var result = Cache.GetOrCreate(cacheKey, TimeSpan.FromSeconds(30), () =>
            {
                var repository = new EfRepository<Product>(DbContext);
                var query = repository.Query();

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(q))
                {
                    query = query.Where(p => p.Name.Contains(q));
                }

                query = sort switch
                {
                    "price" => query.OrderBy(p => p.Price),
                    "-price" => query.OrderByDescending(p => p.Price),
                    "name" => query.OrderBy(p => p.Name),
                    _ => query.OrderBy(p => p.Id)
                };

                var total = query.Count();
                var items = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(ToDto)
                    .ToList();

                return new { total, items };
            });

            var etag = HashUtility.Compute(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return CreateCachedResponse(Request, result, etag, TimeSpan.FromSeconds(60));
        }

        [HttpGet, Route("{id:int}")]
        public async Task<IHttpActionResult> GetProduct(int id)
        {
            var repository = new EfRepository<Product>(DbContext);
            var product = await repository.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var etag = HashUtility.Compute($"product:{product.Id}:{product.Price}:{product.UpdatedAt()}");
            var response = CreateCachedResponse(Request, ToDto(product), etag, TimeSpan.FromSeconds(60));
            return ResponseMessage(response);
        }

        [HttpGet, Route("{id:int}/price-history")]
        public async Task<IHttpActionResult> GetPriceHistory(int id, DateTime? from = null, DateTime? to = null, string bucket = "day")
        {
            var now = DateTime.UtcNow;
            var fromDate = from ?? now.AddDays(-30);
            var toDate = to ?? now;

            var service = ServiceContainer.CreatePriceHistoryService();
            var series = await service.GetSeriesAsync(id, fromDate, toDate, bucket);
            var etag = HashUtility.Compute($"history:{id}:{fromDate:O}:{toDate:O}:{bucket}:{series.Count}");
            var response = CreateCachedResponse(Request, series, etag, TimeSpan.FromSeconds(60));
            return ResponseMessage(response);
        }

        [HttpGet, Route("{id:int}/recommendations")]
        public async Task<IHttpActionResult> GetRecommendations(int id, int take = 10, int skip = 0)
        {
            var cacheKey = $"reco:{id}:{take}:{skip}";
            var recommendations = Cache.GetOrCreate(cacheKey, TimeSpan.FromMinutes(5), () =>
            {
                var service = ServiceContainer.CreateRecommendationService();
                return service.GetRecommendationsAsync(id, take, skip).GetAwaiter().GetResult();
            });

            var etag = HashUtility.Compute($"reco:{id}:{take}:{skip}:{recommendations.Count}");
            var response = CreateCachedResponse(Request, recommendations, etag, TimeSpan.FromSeconds(300));
            return ResponseMessage(response);
        }

        private static ProductDto ToDto(Product product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                CategoryId = product.CategoryId,
                Price = product.Price,
                OldPrice = product.OldPrice,
                Attributes = product.Attributes
            };
        }
    }

    internal static class ProductExtensions
    {
        public static string UpdatedAt(this Product product)
        {
            return $"{product.Price}:{product.OldPrice}:{product.Attributes}";
        }
    }
}
