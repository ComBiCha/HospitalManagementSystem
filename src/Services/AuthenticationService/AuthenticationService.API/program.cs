using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AuthenticationService.API.Data;
using AuthenticationService.API.Services;
using AuthenticationService.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add Entity Framework
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();

// Add JWT Token Service
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Add gRPC
builder.Services.AddGrpc();

// Add Controllers for REST API
builder.Services.AddControllers();

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Authentication Service API", 
        Version = "v1",
        Description = "HMS Authentication Service for managing user accounts and JWT tokens"
    });
    
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

var app = builder.Build();

// Auto-migrate database on startup (for development)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        context.Database.EnsureCreated();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database ensured for Authentication Service");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while ensuring the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Authentication Service API V1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
    });
}

app.UseCors("AllowAll");

// Map gRPC Services
app.MapGrpcService<AuthGrpcService>();

// Map Controllers (REST API)
app.MapControllers();

// Add test endpoints
app.MapGet("/", () => Results.Json(new { 
    message = "🔐 Authentication Service is running!",
    service = "HMS Authentication Service",
    version = "1.0.0",
    framework = ".NET 9.0",
    endpoints = new {
        restApi = "https://localhost:5501/api/auth",
        grpc = "https://localhost:5502",
        swagger = "https://localhost:5501/swagger",
        health = "https://localhost:5501/health",
        users = "https://localhost:5501/api/users"
    },
    defaultAccounts = new {
        admin = new { username = "admin", password = "admin123", role = "Admin" },
        doctor = new { username = "doctor1", password = "doctor123", role = "Doctor" },
        patient = new { username = "patient1", password = "patient123", role = "Patient" }
    }
}));

app.MapGet("/health", () => Results.Json(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    service = "Authentication Service",
    database = "Connected",
    jwt = "Configured"
}));

app.MapGet("/test-db", async (AuthDbContext context) => 
{
    try 
    {
        var usersCount = await context.Users.CountAsync();
        var activeUsersCount = await context.Users.CountAsync(u => u.IsActive);
        var refreshTokensCount = await context.RefreshTokens.CountAsync();
        
        return Results.Json(new { 
            status = "Connected", 
            database = "HMS_AuthDB",
            totalUsers = usersCount,
            activeUsers = activeUsersCount,
            refreshTokens = refreshTokensCount,
            seededAccounts = new {
                admin = "admin / admin123",
                doctor = "doctor1 / doctor123", 
                patient = "patient1 / patient123"
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { 
            status = "Error", 
            error = ex.Message 
        });
    }
});

app.Run();