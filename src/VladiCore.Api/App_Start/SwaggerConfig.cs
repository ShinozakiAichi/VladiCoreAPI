using System.Web.Http;
using Swashbuckle.Application;

namespace VladiCore.Api
{
    public static class SwaggerConfig
    {
        public static void Register()
        {
            GlobalConfiguration.Configuration
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "VladiCore API");
                })
                .EnableSwaggerUi();
        }
    }
}
