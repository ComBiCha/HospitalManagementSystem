using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Infrastructure.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Storage;
using HospitalManagementSystem.Infrastructure.RabbitMQ;
using HospitalManagementSystem.Infrastructure.Channels;
using HospitalManagementSystem.Infrastructure.PaymentFactory;
using HospitalManagementSystem.Infrastructure.PaymentMethods;
using HospitalManagementSystem.Infrastructure.BillingStrategies;
using HospitalManagementSystem.Domain.FhirEpic;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.API.Services;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Strategies;
using HospitalManagementSystem.Domain.Caching;
using HospitalManagementSystem.Domain.Payments;
using HospitalManagementSystem.Domain.Factories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Domain.Storages;
using HospitalManagementSystem.Domain.Notifications;
using StackExchange.Redis;
using Microsoft.OpenApi.Models;
using DotNetEnv;
using HospitalManagementSystem.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// // Load .env
// Env.Load();

// // Áp dụng mapping
// foreach (var pair in EnvKeyMapping.Map)
// {
//     var value = Environment.GetEnvironmentVariable(pair.Key);
//     if (!string.IsNullOrEmpty(value))
//     {
//         builder.Configuration[pair.Value] = value;
//     }
// }

// Kestrel config (nếu cần)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(80);
});

// Add DbContext
builder.Services.AddDbContext<HospitalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis config
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "HMS";
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Repository DI
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<CachedPatientRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddHttpClient<IStorageService, SeaweedStorageService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IBillingRepository, BillingRepository>();

builder.Services.AddHostedService<RabbitMQConsumerService>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, SmsNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, PushNotificationChannel>();

builder.Services.AddScoped<NotificationServiceManager>();

builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

// Redis ConnectionMultiplexer
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    return ConnectionMultiplexer.Connect(configuration);
});

// Background service
builder.Services.AddHostedService<PatientCacheWarmupService>();

// API, Swagger, gRPC, CORS, HealthCheck
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "HMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
            new string[] {}
        }
    });
});
builder.Services.AddGrpc(options => { options.EnableDetailedErrors = true; });
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddHealthChecks()
    .AddCheck("hms-api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API running"))
    .AddCheck("database", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database configured"))
    .AddCheck("redis", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis configured"));

// JWT config
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]))
    };
});

builder.Services.AddScoped<StripePaymentMethod>();
builder.Services.AddScoped<CashPaymentMethod>();
builder.Services.AddScoped<IPaymentFactory, PaymentFactory>();
builder.Services.AddScoped<IBillingStrategyFactory, BillingStrategyFactory>();
builder.Services.AddScoped<InsuranceBillingStrategy>();

builder.Services.AddHttpClient<IFhirEpicIntegrationService, HospitalManagementSystem.Infrastructure.Services.FhirEpicIntegrationService>();
builder.Services.AddScoped<FhirEpicIntegrationService>();
builder.Services.AddScoped<PatientService>();

var app = builder.Build();

// Database migration/check
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        if (await context.Database.CanConnectAsync())
        {
            logger.LogInformation("Database connection successful");
            context.Database.EnsureCreated();
        }
        else
        {
            logger.LogWarning("Database connection failed");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database setup failed: {Error}", ex.Message);
    }
    try
    {
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.SetAsync("startup-test", DateTime.UtcNow.ToString(), TimeSpan.FromMinutes(1));
        logger.LogInformation("Redis cache connection verified");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Redis setup failed: {Error}", ex.Message);
    }
}

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<PatientGrpcService>();
app.MapGrpcService<DoctorGrpcService>();
app.MapGrpcService<AuthGrpcService>();
app.MapHealthChecks("/health");
app.Run();
