using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using VladiCore.Api.Infrastructure;
using VladiCore.Domain.DTOs;
using VladiCore.PcBuilder.Services;

namespace VladiCore.Api.Controllers
{
    [RoutePrefix("api/pc")]
    public class PcController : BaseApiController
    {
        [HttpPost, Route("validate")]
        public async Task<IHttpActionResult> ValidateBuild(PcValidateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Content((HttpStatusCode)422, ModelState);
            }

            var service = ServiceContainer.CreateCompatibilityService(DbContext);
            var result = await service.ValidateAsync(request);
            return Ok(result);
        }

        [HttpPost, Route("autobuild")]
        public async Task<IHttpActionResult> AutoBuild(AutoBuildRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Content((HttpStatusCode)422, ModelState);
            }

            var service = ServiceContainer.CreateAutoBuilderService(DbContext);
            var result = await service.BuildAsync(request);
            return Ok(result);
        }
    }
}
