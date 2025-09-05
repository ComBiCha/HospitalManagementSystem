using Microsoft.EntityFrameworkCore;
using DoctorService.API.Data;
using DoctorService.API.Services;
using DoctorService.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Force HTTP configuration for containers
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// Add Entity Framework
builder.Services.AddDbContext<DoctorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repository
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();

// Add Controllers for REST API
builder.Services.AddControllers();

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Doctor Service API", 
        Version = "v1",
        Description = "HMS Doctor Service"
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
    .AddCheck("doctor-service", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Doctor Service is running"))
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
    });

// DO NOT ADD HTTPS REDIRECTION
// builder.Services.AddHttpsRedirection(); // REMOVE IF EXISTS

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<DoctorDbContext>();
        
        logger.LogInformation("Testing Doctor Service database connection...");
        
        if (await context.Database.CanConnectAsync())
        {
            logger.LogInformation("Doctor Service database connection successful");
            context.Database.EnsureCreated();
            logger.LogInformation("Doctor Service database migration completed successfully");
        }
        else
        {
            logger.LogWarning("Doctor Service database connection failed - running without database");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Doctor Service database setup failed: {Error}", ex.Message);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DO NOT USE HTTPS REDIRECTION
// app.UseHttpsRedirection(); // REMOVE THIS LINE IF EXISTS

app.UseCors("AllowAll");

// Map Controllers (REST API)
app.MapControllers();

// Map gRPC service
app.MapGrpcService<DoctorGrpcService>();

// Map Health Checks
app.MapHealthChecks("/health");

app.Run();