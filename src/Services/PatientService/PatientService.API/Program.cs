using Microsoft.EntityFrameworkCore;
using PatientService.API.Data;
using PatientService.API.Services;
using PatientService.API.Services.Caching;
using PatientService.API.Repositories;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(80, listenOptions =>
    {
        // Chỉ HTTP, không HTTPS
    });
});

// Add Entity Framework
builder.Services.AddDbContext<PatientDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ==================== REDIS CACHE CONFIGURATION ====================
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "PatientService";
});

builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Add Repositories
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<CachedPatientRepository>();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new InvalidOperationException("Redis connection string is required. Please configure 'ConnectionStrings:Redis' in appsettings.json");
}

// Add Redis ConnectionMultiplexer - REQUIRED for RedisCacheService
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    try
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        return ConnectionMultiplexer.Connect(configuration);
    }
    catch (Exception ex)
    {
        var logger = provider.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Failed to connect to Redis: {RedisConnection}", redisConnectionString);
        throw;
    }
});

// Add Redis caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "HMS_PatientService";
});

// Add Redis cache service
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// ==================== REPOSITORY CONFIGURATION - FIXED ====================
// Fix circular dependency: Register base repository with concrete class
builder.Services.AddScoped<PatientRepository>(); 
// Register cached repository but inject base repository as dependency (not interface)
builder.Services.AddScoped<IPatientRepository>(provider =>
{
    var baseRepository = provider.GetRequiredService<PatientRepository>();
    var cacheService = provider.GetRequiredService<ICacheService>();
    var logger = provider.GetRequiredService<ILogger<CachedPatientRepository>>();
    return new CachedPatientRepository(baseRepository, cacheService, logger);
});

// ==================== BACKGROUND SERVICES ====================
// Add Cache Warmup Background Service
builder.Services.AddHostedService<PatientCacheWarmupService>();

// ==================== API CONFIGURATION ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Patient Service API", 
        Version = "v1",
        Description = "HMS Patient Service with Redis Caching"
    });
});

// Add gRPC with HTTP support
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("patient-service", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Patient Service is running"))
    .AddCheck("database", () =>
    {
        try 
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database configured");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database error", ex);
        }
    })
    .AddCheck("redis", () =>
    {
        try
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis configured");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis error", ex);
        }
    });

var app = builder.Build();

// ==================== DATABASE MIGRATION - OPTIONAL FOR LOCAL DEV ====================
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
        
        logger.LogInformation("Testing database connection...");
        
        // Test database connection first
        if (await context.Database.CanConnectAsync())
        {
            logger.LogInformation("Database connection successful");
            context.Database.EnsureCreated();
            logger.LogInformation("Database migration completed successfully");
        }
        else
        {
            logger.LogWarning("Database connection failed - running without database");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database setup failed (normal for local dev without Docker): {Error}", ex.Message);
    }
    
    try
    {
        // Test Redis connection
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.SetAsync("startup-test", DateTime.UtcNow.ToString(), TimeSpan.FromMinutes(1));
        logger.LogInformation("Redis cache connection verified");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Redis setup failed (normal for local dev without Docker): {Error}", ex.Message);
    }
}

// ==================== HTTP PIPELINE CONFIGURATION ====================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Map Controllers (REST API)
app.MapControllers();

// Map gRPC service
app.MapGrpcService<PatientGrpcService>();

// Map Health Checks
app.MapHealthChecks("/health");

app.Run();