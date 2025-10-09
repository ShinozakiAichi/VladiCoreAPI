using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers
{
    [Authorize]
    [RoutePrefix("api/products")]
    public class AdminProductsController : BaseApiController
    {
        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Upsert(ProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Content((HttpStatusCode)422, ModelState);
            }

            var repository = new EfRepository<Product>(DbContext);
            if (dto.Id == 0)
            {
                var product = new Product
                {
                    Sku = dto.Sku,
                    Name = dto.Name,
                    CategoryId = dto.CategoryId,
                    Price = dto.Price,
                    OldPrice = dto.OldPrice,
                    Attributes = dto.Attributes,
                    CreatedAt = System.DateTime.UtcNow
                };

                await repository.AddAsync(product);
            }
            else
            {
                var product = await repository.FindAsync(dto.Id);
                if (product == null)
                {
                    return NotFound();
                }

                product.Sku = dto.Sku;
                product.Name = dto.Name;
                product.CategoryId = dto.CategoryId;
                product.Price = dto.Price;
                product.Attributes = dto.Attributes;
            }

            await repository.SaveChangesAsync();
            Cache.RemoveByPrefix("products:");
            Cache.RemoveByPrefix($"reco:{dto.Id}:");

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
