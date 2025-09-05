using Microsoft.AspNetCore.Mvc;

namespace HMS.ApiGateway.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly HttpClient _httpClient;

    public HealthController(ILogger<HealthController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var services = new Dictionary<string, object>();

        // Check Patient Service
        services.Add("PatientService", await CheckServiceHealth("http://patient-service:80/health"));
        
        // Check Doctor Service  
        services.Add("DoctorService", await CheckServiceHealth("http://doctor-service:80/health"));
        
        // Check Billing Service
        services.Add("BillingService", await CheckServiceHealth("http://billing-service:80/health"));

        var overallStatus = services.Values.All(s => ((dynamic)s).status == "Healthy") ? "Healthy" : "Unhealthy";

        return Ok(new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow,
            gateway = "HMS API Gateway",
            version = "1.0.0",
            services = services
        });
    }

    private async Task<object> CheckServiceHealth(string healthUrl)
    {
        try
        {
            _logger.LogDebug("Checking health for: {HealthUrl}", healthUrl);
            
            var response = await _httpClient.GetAsync(healthUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new { status = "Healthy", response = content };
            }
            else
            {
                return new { status = "Unhealthy", statusCode = response.StatusCode };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for {HealthUrl}", healthUrl);
            return new { status = "Unhealthy", error = ex.Message };
        }
    }
}