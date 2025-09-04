using Microsoft.EntityFrameworkCore;
using DoctorService.API.Data;
using DoctorService.API.Services;
using DoctorService.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add Entity Framework
builder.Services.AddDbContext<DoctorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repository
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();

// Add Controllers for REST API
builder.Services.AddControllers();

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add gRPC
builder.Services.AddGrpc();

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
        var context = scope.ServiceProvider.GetRequiredService<DoctorDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Doctor Service API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");
app.UseRouting();

// Map Controllers (REST API)
app.MapControllers();

// Map gRPC service
app.MapGrpcService<DoctorGrpcService>();

// Add test endpoints
app.MapGet("/", () => Results.Json(new { 
    message = "Doctor Service is running!",
    endpoints = new {
        restApi = "https://localhost:5201/api/doctors",
        swagger = "https://localhost:5201/swagger",
        grpc = "https://localhost:5202 (Use gRPC client to connect)",
        health = "https://localhost:5201/health",
        testDb = "https://localhost:5201/test-db"
    }
}));

app.MapGet("/health", () => Results.Json(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    service = "Doctor Service",
    endpoints = new {
        restApi = "https://localhost:5201",
        grpc = "https://localhost:5202"
    }
}));

app.MapGet("/test-db", async (DoctorDbContext context) => 
{
    try 
    {
        var count = await context.Doctors.CountAsync();
        return Results.Json(new { 
            status = "Connected", 
            doctorsCount = count,
            database = "HMS_DoctorDB"
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
