using Microsoft.EntityFrameworkCore;
using NotificationService.API.Data;
using NotificationService.API.Services.RabbitMQ;
using NotificationService.API.Services.Interfaces;
using NotificationService.API.Services.Channels;
using NotificationService.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add Entity Framework
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Controllers for REST API
builder.Services.AddControllers();

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add RabbitMQ Consumer as Hosted Service
builder.Services.AddHostedService<RabbitMQConsumerService>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, SmsNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, PushNotificationChannel>();

builder.Services.AddScoped<NotificationServiceManager>();

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
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");
app.UseRouting();

// Map Controllers (REST API)
app.MapControllers();

// Add test endpoints
app.MapGet("/", () => Results.Json(new { 
    message = "Notification Service is running!",
    endpoints = new {
        restApi = "https://localhost:5401/api/notifications",
        swagger = "https://localhost:5401/swagger",
        health = "https://localhost:5401/health",
        testDb = "https://localhost:5401/test-db",
        rabbitmq = "Consuming from hospital.events exchange"
    }
}));

app.MapGet("/health", () => Results.Json(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    service = "Notification Service",
    rabbitMQ = "Consuming appointment events"
}));

app.MapGet("/test-db", async (NotificationDbContext context) => 
{
    try 
    {
        var count = await context.Notifications.CountAsync();
        return Results.Json(new { 
            status = "Connected", 
            notificationsCount = count,
            database = "HMS_NotificationDB"
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
