using System.Text.Json.Serialization;
using CabinetLogs.Api.Extensions;
using CabinetLogs.Api.Middleware;
using CabinetLogs.Application;
using CabinetLogs.Infrastructure;
using CabinetLogs.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add local unversioned config override if present (ignored by git and Docker)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Bind to the PORT injected by Render (or other PaaS providers) when ASPNETCORE_URLS is not explicitly set.
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort)
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// Configure multipart/form-data limits to support multiple photos per log while keeping bounds safe.
var maxPhotoBytes = builder.Configuration.GetValue<long?>("PhotoUpload:MaxFileSizeBytes") ?? (5 * 1024 * 1024);
var maxPhotosPerLog = builder.Configuration.GetValue<int?>("PhotoUpload:MaxPhotosPerLog") ?? 10;
var maxRequestBodyBytes = (maxPhotoBytes * maxPhotosPerLog) + (2 * 1024 * 1024);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBodyBytes;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCabinetLogsOpenApi();

// CORS configuration (open in development or restricted via Cors:AllowedOrigins in production)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("CabinetLogsCors", policy =>
    {
        if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedContentDisposition();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedContentDisposition();
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Optional automatic EF Core migration on startup (enable via Database__AutoMigrate=true if desired)
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CabinetLogsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!app.Environment.IsDevelopment() && !runningInContainer)
{
    app.UseHsts();
}

// Render terminates TLS at the edge proxy and forwards HTTP internally.
if (!runningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseCors("CabinetLogsCors");
app.UseMiddleware<OptionalApiKeyMiddleware>();

var enableScalar = app.Configuration.GetValue<bool?>("Scalar:Enabled") ?? app.Environment.IsDevelopment();
if (enableScalar)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Cabinet Logs API — v1")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    app.MapGet("/", () => Results.Redirect("/scalar"))
       .ExcludeFromDescription();
}
else
{
    app.MapGet("/", () => Results.Ok(new
    {
        service = "Cabinet Logs API",
        version = "v1",
        health = "/api/v1/health"
    })).ExcludeFromDescription();
}

app.MapControllers();

app.Run();

public partial class Program;

internal static class CorsPolicyBuilderExtensions
{
    public static Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder WithExposedContentDisposition(
        this Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder builder)
    {
        return builder.WithExposedHeaders("Content-Disposition");
    }
}
