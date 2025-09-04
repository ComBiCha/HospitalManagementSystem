using Microsoft.EntityFrameworkCore;
using PatientService.API.Data;
using PatientService.API.Services;
using PatientService.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add Entity Framework
builder.Services.AddDbContext<PatientDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repository
builder.Services.AddScoped<IPatientRepository, PatientRepository>();

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
        var context = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Patient Service API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");
app.UseRouting();

// Map Controllers (REST API)
app.MapControllers();

// Map gRPC service
app.MapGrpcService<PatientGrpcService>();

// Add test endpoints
app.MapGet("/", () => Results.Json(new { 
    message = "Patient Service is running!",
    endpoints = new {
        restApi = "https://localhost:5101/api/patients",
        swagger = "https://localhost:5101/swagger",
        grpc = "https://localhost:5102 (Use gRPC client to connect)",
        health = "https://localhost:5101/health",
        testDb = "https://localhost:5101/test-db"
    }
}));

app.MapGet("/health", () => Results.Json(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    service = "Patient Service",
    endpoints = new {
        restApi = "https://localhost:5101",
        grpc = "https://localhost:5102"
    }
}));

app.MapGet("/test-db", async (PatientDbContext context) => 
{
    try 
    {
        var count = await context.Patients.CountAsync();
        return Results.Json(new { 
            status = "Connected", 
            patientsCount = count,
            database = "HMS_PatientDB"
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
