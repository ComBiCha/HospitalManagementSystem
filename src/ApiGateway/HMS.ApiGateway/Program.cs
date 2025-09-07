using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(80, listenOptions =>
    {
        // Chỉ HTTP, không HTTPS
    });
});

// Add services to the container
builder.Services.AddControllers();

// ADD HTTPCLIENT SERVICE - QUAN TRỌNG!
builder.Services.AddHttpClient();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "HMS API Gateway", 
        Version = "v1" 
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

// Add Health Checks
builder.Services.AddHealthChecks();

// Add Ocelot
var ocelotConfig = builder.Configuration.GetSection("Ocelot");
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("ocelot.development.json", optional: true, reloadOnChange: true);
}

builder.Services.AddOcelot();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HMS API Gateway V1");
    });
}

app.UseCors("AllowAll");

// Use Ocelot
await app.UseOcelot();

app.Run();