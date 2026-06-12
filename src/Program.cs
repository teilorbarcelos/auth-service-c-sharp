using dotenv.net;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using MageBackend.Web;
using MageBackend.Infrastructure.Configuration;
using MageBackend.Database;
using MageBackend.Infrastructure.Auth;
using MageBackend.Web.Middleware;
using FluentValidation;
using Serilog;
using Serilog.Events;

var envFiles = new[] { "../.env", ".env" };
DotEnv.Load(options: new DotEnvOptions(envFilePaths: envFiles, ignoreExceptions: true));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {TraceId}{Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Auth Service...");

    var builder = WebApplication.CreateBuilder(args);

    var shutdownTimeout = int.TryParse(Environment.GetEnvironmentVariable("SHUTDOWN_TIMEOUT_SECONDS"), out var st) && st > 0 ? st : 30;
    builder.Host.ConfigureHostOptions(o => o.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeout));
    Log.Information("[Host] Shutdown timeout configured to {Timeout}s", shutdownTimeout);

    builder.Host.UseSerilog();

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8001";
#pragma warning disable S5332
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
#pragma warning restore S5332

    var dbUrl = EnvValidator.Required("DATABASE_URL");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(dbUrl));

    var redisUrl = EnvValidator.RequiredAny("REDIS_URL", "REDIS_HOST");
    RedisProvider.Initialize(redisUrl);

    var jwtSecret = EnvValidator.Required("JWT_SECRET");
    builder.Services.AddSingleton(new JwtProvider(jwtSecret));

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Servers = new List<Microsoft.OpenApi.OpenApiServer>
            {
                new() { Url = "v1/" }
            };
            return Task.CompletedTask;
        });
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "v1");
            options.RoutePrefix = "v1/docs";
        });
    }

    app.UseMiddleware<ErrorHandlerMiddleware>();
    app.UseCors("Default");
    app.UseMiddleware<RequestLoggingMiddleware>();

    var disableRateLimit = Environment.GetEnvironmentVariable("DISABLE_RATE_LIMIT") is string dr && (dr.Equals("true", StringComparison.OrdinalIgnoreCase) || dr == "1");
    if (!disableRateLimit)
    {
        app.UseMiddleware<RateLimitMiddleware>();
    }

    app.UseMiddleware<JwtAuthenticationMiddleware>();
    app.UseMiddleware<TokenSessionValidationMiddleware>();

    app.UseRouting();
    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("o") }));
    app.MapGet("/liveness", () => Results.Ok(new { status = "alive", uptime = Environment.TickCount64 }));
    app.MapGet("/ready", () => Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow.ToString("o") }));

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.InitializeAsync(dbContext);
    }

    Log.Information("Server ready at http://localhost:{Port} | Docs: http://localhost:{Port}/v1/docs", port, port);

    await app.RunAsync();
}
catch (Exception ex) when (ex.GetType().Name == "HostAbortedException")
{
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
