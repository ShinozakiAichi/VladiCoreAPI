using System;
using System.Configuration;
using System.Text;
using System.Web.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Security.Jwt;
using Owin;

[assembly: OwinStartup(typeof(VladiCore.Api.Startup))]

namespace VladiCore.Api
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureJwt(app);
            var config = GlobalConfiguration.Configuration;
            app.UseWebApi(config);
        }

        private static void ConfigureJwt(IAppBuilder app)
        {
            var issuer = ConfigurationManager.AppSettings["JwtIssuer"];
            var audience = ConfigurationManager.AppSettings["JwtAudience"];
            var signingKey = ConfigurationManager.AppSettings["JwtSigningKey"];

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException("JwtSigningKey is not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            app.UseJwtBearerAuthentication(new JwtBearerAuthenticationOptions
            {
                AuthenticationMode = Microsoft.Owin.Security.AuthenticationMode.Active,
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromMinutes(2)
                }
            });
        }
    }
}
