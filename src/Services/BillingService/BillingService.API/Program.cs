using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using BillingService.API.Data;
using BillingService.API.Models;
using BillingService.API.Repositories;
using BillingService.API.Services.PaymentMethods;
using BillingService.API.Services.PaymentFactory;
using BillingService.API.Services.BillingStrategies;
using BillingService.API.Services.RabbitMQ;
using BillingService.API.Services.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for HMS microservices
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Entity Framework with enhanced configuration for HMS
builder.Services.AddDbContext<BillingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
    
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add JWT Configuration for HMS Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? "HMS_SuperSecretKey_ForDevelopment_2024_MustBe32CharsOrMore!";

// Add JWT Authentication for HMS microservices
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };

    // Enable JWT for gRPC following HMS inter-service communication
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && 
                (path.StartsWithSegments("/grpc") || 
                 context.Request.ContentType?.Contains("application/grpc") == true))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "JWT Authentication failed for HMS Billing Service");
            return Task.CompletedTask;
        }
    };
});

// Add Authorization policies for HMS role-based access control
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DoctorOrAdmin", policy => policy.RequireRole("Doctor", "Admin"));
    options.AddPolicy("PatientAccess", policy => policy.RequireRole("Patient", "Doctor", "Admin"));
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

// Add Repository following Clean Architecture
builder.Services.AddScoped<IBillingRepository, BillingRepository>();

// Payment Methods
builder.Services.AddScoped<StripePaymentMethod>();
builder.Services.AddScoped<CashPaymentMethod>();

// Payment Factory
builder.Services.AddScoped<IPaymentFactory, PaymentFactory>();

// Add RabbitMQ Service for HMS async messaging
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

builder.Services.AddScoped<InsuranceBillingStrategy>();
// builder.Services.AddScoped<SelfPayBillingStrategy>();
// builder.Services.AddScoped<CorporateBillingStrategy>();
builder.Services.AddScoped<IBillingStrategyFactory, BillingStrategyFactory>();

// Add Controllers for REST API
builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});

// Add API Explorer for Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "HMS Billing Service API", 
        Version = "v1",
        Description = "Hospital Management System - Billing Service with Factory pattern for payment processing",
        Contact = new OpenApiContact
        {
            Name = "HMS Development Team",
            Email = "dev@hospital.com"
        }
    });

    // Add JWT Authentication to Swagger following HMS standards
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Add gRPC with authentication support for HMS microservices
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaxReceiveMessageSize = 1024 * 1024 * 4; // 4MB
    options.MaxSendMessageSize = 1024 * 1024 * 4; // 4MB
});

// Enhanced CORS for HMS development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
    
    options.AddPolicy("HMS_Development", policy =>
    {
        policy.WithOrigins("https://localhost:3000", "http://localhost:3000") // React frontend
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Health Checks for HMS monitoring
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddCheck("billing-service", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("HMS Billing Service is running"));

var app = builder.Build();

// Auto-migrate database on startup (for development following HMS patterns)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await context.Database.EnsureCreatedAsync();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("HMS Billing Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the HMS Billing Database");
    }
}

// Configure the HTTP request pipeline following HMS standards
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HMS Billing Service API V1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "HMS Billing Service API";
        c.DisplayRequestDuration();
    });
}

// Security middleware in correct order for HMS
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();

// CRITICAL: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers (REST API) following Clean Architecture
app.MapControllers();

// Map gRPC service for HMS inter-service communication
app.MapGrpcService<BillingGrpcService>();

// Enhanced health check endpoints for HMS monitoring
app.MapHealthChecks("/health");

app.MapGet("/health/detailed", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    return Results.Ok(new
    {
        Service = "HMS Billing Service",
        Status = report.Status.ToString(),
        Checks = report.Entries.Select(entry => new
        {
            Name = entry.Key,
            Status = entry.Value.Status.ToString(),
            Description = entry.Value.Description,
            Duration = entry.Value.Duration.TotalMilliseconds
        }),
        TotalDuration = report.TotalDuration.TotalMilliseconds,
        Timestamp = DateTime.UtcNow
    });
});

// Enhanced service info endpoint for HMS service discovery
app.MapGet("/", () => Results.Json(new { 
    Service = "HMS Billing Service",
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName,
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Architecture = "Clean Architecture + Microservices",
    Authentication = "JWT Bearer Token",
    Message = "Hospital Management System - Billing Service with Factory Pattern is running!",
    Endpoints = new {
        RestApi = "https://localhost:7001/api/billings",
        Swagger = "https://localhost:7001/swagger",
        Grpc = "https://localhost:7002 (Use gRPC client to connect)",
        Health = "https://localhost:7001/health",
        DetailedHealth = "https://localhost:7001/health/detailed",
        TestDb = "https://localhost:7001/test-db",
        PaymentMethods = "https://localhost:7001/api/billings/payment-methods",
        TestEndpoints = new
        {
            GenerateToken = "https://localhost:7001/api/test/generate-token",
            ValidateToken = "https://localhost:7001/api/test/validate-token",
            ServiceInfo = "https://localhost:7001/api/test/service-info"
        }
    },
    Features = new {
        Authentication = "JWT Bearer",
        Authorization = "Role-based (Admin, Doctor, Patient)",
        Database = "PostgreSQL",
        Messaging = "RabbitMQ",
        InterServiceCommunication = "gRPC",
        ApiDocumentation = "Swagger/OpenAPI",
        PaymentMethods = new[] { "Stripe", "Cash" },
        FactoryPattern = "Payment method factory for extensibility",
        TestingSupport = "JWT token generation and validation endpoints"
    }
}));

// Protected health endpoint requiring authentication
app.MapGet("/health/secure", () => Results.Json(new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Service = "HMS Billing Service",
    User = "Authenticated",
    Endpoints = new {
        RestApi = "https://localhost:7001",
        Grpc = "https://localhost:7002"
    }
})).RequireAuthorization("AuthenticatedUser");

// Enhanced database test endpoint for HMS
app.MapGet("/test-db", async (BillingDbContext context) => 
{
    try 
    {
        var canConnect = await context.Database.CanConnectAsync();
        var count = await context.Billings.CountAsync();
        
        return Results.Json(new { 
            Status = canConnect ? "Connected" : "Connection Failed",
            BillingsCount = count,
            Database = "HMS_BillingDB",
            Provider = "PostgreSQL",
            Timestamp = DateTime.UtcNow,
            Environment = app.Environment.EnvironmentName
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { 
            Status = "Error", 
            Error = ex.Message,
            Database = "HMS_BillingDB",
            Timestamp = DateTime.UtcNow
        });
    }
});

app.Logger.LogInformation("HMS Billing Service started successfully at {Time}", DateTime.UtcNow);
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("JWT Authentication: Enabled");
app.Logger.LogInformation("Available endpoints: REST API, gRPC, Swagger, Health Checks");
app.Logger.LogInformation("Payment Methods: Stripe, Cash (Factory Pattern)");

app.Run();
