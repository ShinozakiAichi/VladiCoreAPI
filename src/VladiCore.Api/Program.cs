using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Middleware;
using VladiCore.Data.Contexts;
using VladiCore.Data.Infrastructure;
using VladiCore.PcBuilder.Services;
using VladiCore.Recommendations.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day);
});

var connectionString = config.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
builder.Services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
builder.Services.AddScoped<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IPriceHistoryService, PriceHistoryService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPcCompatibilityService, PcCompatibilityService>();
builder.Services.AddScoped<IPcAutoBuilderService, PcAutoBuilderService>();

var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtSection = config.GetSection("Jwt");
var signingKey = jwtSection.GetValue<string>("SigningKey");
if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseRequestLogging();

app.UseCors("default");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
