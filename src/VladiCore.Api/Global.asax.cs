using System;
using System.IO;
using System.Web;
using System.Web.Http;
using Serilog;

namespace VladiCore.Api
{
    public class Global : HttpApplication
    {
        protected void Application_Start()
        {
            ConfigureSerilog();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            SwaggerConfig.Register();
        }

        private static void ConfigureSerilog()
        {
            var logDirectory = HttpRuntime.AppDomainAppPath != null
                ? Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "logs")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "logs");
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(logDirectory, "log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
            };
        }

        protected void Application_End()
        {
            Log.CloseAndFlush();
        }
    }
}
