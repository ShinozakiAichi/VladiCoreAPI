using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Infrastructure.Database;
using VladiCore.Api.Infrastructure.ObjectStorage;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Api.Middleware;
using VladiCore.Api.Swagger;
using VladiCore.Api.Services;
using VladiCore.Data.Contexts;
using VladiCore.Data.Identity;
using VladiCore.Data.Infrastructure;
using VladiCore.Data.Provisioning;
using VladiCore.Domain.Services;
using VladiCore.PcBuilder.Services;
using VladiCore.Recommendations.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

const string AllowAllCorsPolicyName = "AllowAll";

// ===============================
// 🌍 Глобальный CORS (разрешить всех)
// ===============================
builder.Services.AddCors(options =>
{
    options.AddPolicy(AllowAllCorsPolicyName, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

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

builder.Services.Configure<ReviewOptions>(config.GetSection("Reviews"));
builder.Services.Configure<S3Options>(config.GetSection("S3"));
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
builder.Services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
builder.Services.Configure<DatabaseProvisioningOptions>(config.GetSection("Database:AutoProvision"));
builder.Services.AddSingleton<ISqlScriptDirectoryScanner, SqlScriptDirectoryScanner>();
builder.Services.AddSingleton<ISchemaBootstrapper, SchemaBootstrapper>();
builder.Services.AddScoped<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IPriceHistoryService, PriceHistoryService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPcCompatibilityService, PcCompatibilityService>();
builder.Services.AddScoped<IPcAutoBuilderService, PcAutoBuilderService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3Options>>().Value;
    if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
    {
        throw new InvalidOperationException("S3 credentials must be configured.");
    }

    if (string.IsNullOrWhiteSpace(options.Endpoint))
    {
        throw new InvalidOperationException("S3 endpoint must be configured.");
    }

    var s3Config = new AmazonS3Config
    {
        ForcePathStyle = true,
        UseHttp = !options.UseSsl
    };

    if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri))
    {
        s3Config.ServiceURL = endpointUri.ToString();
    }
    else
    {
        var scheme = options.UseSsl ? "https" : "http";
        s3Config.ServiceURL = $"{scheme}://{options.Endpoint}";
    }

    var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
    return new AmazonS3Client(credentials, s3Config);
});

builder.Services.AddSingleton<IObjectStorageService, S3StorageService>();
builder.Services.AddHostedService<DatabaseProvisioningHostedService>();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VladiCore API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    c.SchemaFilter<RequestExamplesSchemaFilter>();
});

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
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("User", policy => policy.RequireRole("User", "Admin"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRequestLogging();

// ===============================
// 🌍 Включаем CORS до аутентификации
// ===============================
app.UseCors(AllowAllCorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
