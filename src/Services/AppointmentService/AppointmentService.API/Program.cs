using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AppointmentService.API.Data;
using AppointmentService.API.Models;
using AppointmentService.API.Services;
using AppointmentService.API.Repositories;
using AppointmentService.API.Services.GrpcClients;
using AppointmentService.API.Services.RabbitMQ;
using AppointmentService.API.Grpc.Clients;
using Grpc.Net.Client;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for HMS microservices
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Entity Framework with enhanced configuration for HMS
builder.Services.AddDbContext<AppointmentDbContext>(options =>
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

// ✅ Add JWT Configuration for HMS Authentication
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? new JwtSettings
    {
        SecretKey = "HMS_SuperSecretKey_ForDevelopment_2024_MustBe32CharsOrMore!",
        Issuer = "HMS.AuthService",
        Audience = "HMS.Services",
        ExpiryMinutes = 60
    };

// ✅ Add JWT Authentication for HMS microservices
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
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
            logger.LogError(context.Exception, "JWT Authentication failed for HMS Appointment Service");
            return Task.CompletedTask;
        }
    };
});

// ✅ Add Authorization policies for HMS role-based access control
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DoctorOrAdmin", policy => policy.RequireRole("Doctor", "Admin"));
    options.AddPolicy("PatientAccess", policy => policy.RequireRole("Patient", "Doctor", "Admin"));
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

// Add Repository following Clean Architecture
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();

// Add JWT Token Service for HMS authentication
builder.Services.AddHttpClient<IJwtTokenService, JwtTokenService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Add RabbitMQ Service for HMS async messaging
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

// Add gRPC Clients for HMS inter-service communication
builder.Services.AddSingleton(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var config = provider.GetRequiredService<IConfiguration>();
    var patientServiceUrl = config["GrpcClients:PatientService"] ?? "https://localhost:5102";
    
    logger.LogInformation("Configuring HMS Patient gRPC client for {Url}", patientServiceUrl);
    
    var channel = GrpcChannel.ForAddress(patientServiceUrl, new GrpcChannelOptions
    {
        HttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }
    });
    
    return new PatientGrpcService.PatientGrpcServiceClient(channel);
});

builder.Services.AddSingleton(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var config = provider.GetRequiredService<IConfiguration>();
    var doctorServiceUrl = config["GrpcClients:DoctorService"] ?? "https://localhost:5202";
    
    logger.LogInformation("Configuring HMS Doctor gRPC client for {Url}", doctorServiceUrl);
    
    var channel = GrpcChannel.ForAddress(doctorServiceUrl, new GrpcChannelOptions
    {
        HttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }
    });
    
    return new DoctorGrpcService.DoctorGrpcServiceClient(channel);
});

// Add gRPC Client wrappers following Clean Architecture
builder.Services.AddScoped<IPatientGrpcClient, PatientGrpcClient>();
builder.Services.AddScoped<IDoctorGrpcClient, DoctorGrpcClient>();

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
        Title = "HMS Appointment Service API", 
        Version = "v1",
        Description = "Hospital Management System - Appointment Service with JWT Authentication",
        Contact = new OpenApiContact
        {
            Name = "HMS Development Team",
            Email = "dev@hospital.com"
        }
    });

    // ✅ Add JWT Authentication to Swagger following HMS standards
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

// ✅ Enhanced CORS for HMS development
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

// ✅ Add Health Checks for HMS monitoring
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddCheck("appointment-service", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("HMS Appointment Service is running"));

var app = builder.Build();

// Auto-migrate database on startup (for development following HMS patterns)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        await context.Database.EnsureCreatedAsync();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("HMS Appointment Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the HMS Appointment Database");
    }
}

// Configure the HTTP request pipeline following HMS standards
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HMS Appointment Service API V1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "HMS Appointment Service API";
        c.DisplayRequestDuration();
    });
}

// ✅ Security middleware in correct order for HMS
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();

// ✅ CRITICAL: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers (REST API) following Clean Architecture
app.MapControllers();

// Map gRPC service for HMS inter-service communication
app.MapGrpcService<AppointmentGrpcService>();

// ✅ Enhanced health check endpoints for HMS monitoring
app.MapHealthChecks("/health");

app.MapGet("/health/detailed", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    return Results.Ok(new
    {
        Service = "HMS Appointment Service",
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

// ✅ Enhanced service info endpoint for HMS service discovery
app.MapGet("/", () => Results.Json(new { 
    Service = "HMS Appointment Service",
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName,
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Architecture = "Clean Architecture + Microservices",
    Authentication = "JWT Bearer Token",
    Message = "Hospital Management System - Appointment Service is running!",
    Endpoints = new {
        RestApi = "https://localhost:5301/api/appointments",
        Swagger = "https://localhost:5301/swagger",
        Grpc = "https://localhost:5302 (Use gRPC client to connect)",
        Health = "https://localhost:5301/health",
        DetailedHealth = "https://localhost:5301/health/detailed",
        TestDb = "https://localhost:5301/test-db",
        RabbitMQ = "http://localhost:15672 (guest/guest)"
    },
    Features = new {
        Authentication = "JWT Bearer",
        Authorization = "Role-based (Admin, Doctor, Patient)",
        Database = "PostgreSQL",
        Messaging = "RabbitMQ",
        InterServiceCommunication = "gRPC",
        ApiDocumentation = "Swagger/OpenAPI"
    },
    ConnectedServices = new {
        PatientService = "https://localhost:5101 (REST), https://localhost:5102 (gRPC)",
        DoctorService = "https://localhost:5201 (REST), https://localhost:5202 (gRPC)",
        BillingService = "https://localhost:7001 (REST), https://localhost:7002 (gRPC)",
        AuthService = "https://localhost:6001"
    }
}));

// ✅ Protected health endpoint requiring authentication
app.MapGet("/health/secure", () => Results.Json(new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Service = "HMS Appointment Service",
    User = "Authenticated",
    Endpoints = new {
        RestApi = "https://localhost:5301",
        Grpc = "https://localhost:5302"
    }
})).RequireAuthorization("AuthenticatedUser");

// Enhanced database test endpoint for HMS
app.MapGet("/test-db", async (AppointmentDbContext context) => 
{
    try 
    {
        var canConnect = await context.Database.CanConnectAsync();
        var count = await context.Appointments.CountAsync();
        
        return Results.Json(new { 
            Status = canConnect ? "Connected" : "Connection Failed",
            AppointmentsCount = count,
            Database = "HMS_AppointmentDB",
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
            Database = "HMS_AppointmentDB",
            Timestamp = DateTime.UtcNow
        });
    }
});

// ✅ JWT Token validation endpoint for HMS inter-service communication
app.MapPost("/auth/validate", async (HttpRequest request, IJwtTokenService jwtService) =>
{
    try
    {
        var token = request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        var isValid = await jwtService.ValidateTokenAsync(token);
        if (isValid)
        {
            return Results.Ok(new { Valid = true, Message = "Token is valid", Service = "HMS Appointment Service" });
        }
        
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Token validation error: {ex.Message}");
    }
});

app.Logger.LogInformation("HMS Appointment Service started successfully at {Time}", DateTime.UtcNow);
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("JWT Authentication: Enabled");
app.Logger.LogInformation("Available endpoints: REST API, gRPC, Swagger, Health Checks");

app.Run();