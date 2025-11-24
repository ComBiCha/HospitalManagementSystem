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
// using HospitalManagementSystem.Infrastructure.BillingStrategies;
using HospitalManagementSystem.Infrastructure.Epic;
using HospitalManagementSystem.Infrastructure.Cerner;
using HospitalManagementSystem.Infrastructure.FhirFactory;
using HospitalManagementSystem.Infrastructure.Dicom;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.API.Services;
using HospitalManagementSystem.Domain.Repositories;
// using HospitalManagementSystem.Domain.Strategies;
using HospitalManagementSystem.Domain.Caching;
using HospitalManagementSystem.Domain.Factories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Domain.Storages;
using HospitalManagementSystem.Domain.Notifications;
using HospitalManagementSystem.Domain.Dicom;
using StackExchange.Redis;
using Microsoft.OpenApi.Models;
using DotNetEnv;
using HospitalManagementSystem.Infrastructure.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Dashboard;
using Stripe;
using HospitalManagementSystem.Infrastructure.Services;
using HospitalManagementSystem.Domain.Payments;
using HospitalManagementSystem.API.Hubs; // Add this using statement
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Add custom json configuration file
builder.Configuration.AddJsonFile("src/advance_payment_suggestions.json", optional: true, reloadOnChange: true);

// Load .env
Env.Load();

// Áp dụng mapping
foreach (var pair in EnvKeyMapping.Map)
{
    var value = Environment.GetEnvironmentVariable(pair.Key);
    if (!string.IsNullOrEmpty(value))
    {
        builder.Configuration[pair.Value] = value;
    }
}

// Kestrel config 
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
builder.Services.AddScoped<IStorageService, SeaweedStorageService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>(); // Add this
// builder.Services.AddScoped<IBillingRepository, BillingRepository>();
builder.Services.AddScoped<IDoctorShiftRepository, DoctorShiftRepository>();
builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IChatParticipantRepository, ChatParticipantRepository>();

builder.Services.AddHostedService<RabbitMQConsumerService>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, SmsNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, PushNotificationChannel>();

builder.Services.AddScoped<NotificationServiceManager>();

builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

builder.Services.AddScoped<IDoctorAttendanceRepository, DoctorAttendanceRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<DoctorAttendanceNotificationService>();

builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<IPrescriptionItemRepository, PrescriptionItemRepository>();
builder.Services.AddScoped<IMedicalRecordHistoryRepository, MedicalRecordHistoryRepository>();
builder.Services.AddScoped<MedicalRecordApplicationService>();
builder.Services.AddScoped<AccountantApplicationService>();
builder.Services.AddScoped<AppointmentApplicationService>();
builder.Services.AddScoped<AppointmentExaminationService>();
builder.Services.AddScoped<AppointmentTools>();
builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();

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

// Read Frontend URL from configuration
var frontendUrl = builder.Configuration["APP-FRONTENDURL"];
if (string.IsNullOrEmpty(frontendUrl))
{
    // Use a default or throw an exception if the URL is critical
    frontendUrl = "http://localhost:3000"; // Default for local dev if not set
    Console.WriteLine("Warning: APP-FRONTENDURL not set. Defaulting to http://localhost:3000 for CORS.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
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

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddScoped<StripePaymentMethod>();
builder.Services.AddScoped<CashPaymentMethod>();
builder.Services.AddScoped<IPaymentFactory, PaymentFactory>();
// builder.Services.AddScoped<IBillingStrategyFactory, BillingStrategyFactory>();
// builder.Services.AddScoped<InsuranceBillingStrategy>();

builder.Services.AddHttpClient<EpicFhirIntegrationService>(client =>
{
    var epicConfig = builder.Configuration.GetSection("EpicFhir");
    var baseUrl = epicConfig["BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/fhir+json"));
});
builder.Services.AddHttpClient<CernerFhirIntegrationService>();
builder.Services.AddScoped<EhrFhirIntegrationFactory>();
builder.Services.AddScoped<EhrFhirApplicationService>();

builder.Services.AddScoped<IDicomService, DicomService>();
builder.Services.AddScoped<DicomApplicationService>();

builder.Services.AddScoped<PatientService>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// MCP Server Registration
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Configure Stripe globally
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
    dbContext.Database.Migrate();
}

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets(); // Enable WebSockets

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllAuthorizationFilter() }
});

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMcp(); // Map MCP endpoint
app.MapGrpcService<PatientGrpcService>();
app.MapGrpcService<DoctorGrpcService>();
app.MapGrpcService<AuthGrpcService>();
app.MapHealthChecks("/health");
app.MapHub<ChatHub>("/chatHub");
// Initialize attendance notification recurring jobs
using (var scope = app.Services.CreateScope())
{
    var attendanceService = scope.ServiceProvider.GetRequiredService<DoctorAttendanceNotificationService>();
    attendanceService.InitializeRecurringJobs();
    
    // Schedule today's notifications immediately on startup
    await attendanceService.ScheduleCheckInNotificationsAsync();
}

app.Run();

// Allow all access to Hangfire Dashboard (for development only)
public class AllowAllAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true; // Allow all access for testing
    }
}

// For production use this:
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        return httpContext.User.Identity?.IsAuthenticated == true && 
               httpContext.User.IsInRole("Admin");
    }
}
