using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BillingService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestController> _logger;

        public TestController(IConfiguration configuration, ILogger<TestController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Generate JWT token for testing purposes in HMS BillingService
        /// </summary>
        [HttpPost("generate-token")]
        public ActionResult<object> GenerateTestToken(GenerateTokenRequest request)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("Jwt");
                var secretKey = jwtSettings["SecretKey"] ?? "HMS_SuperSecretKey_ForDevelopment_2024_MustBe32CharsOrMore!";
                var issuer = jwtSettings["Issuer"] ?? "HMS.AuthService";
                var audience = jwtSettings["Audience"] ?? "HMS.Services";

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
                    new Claim(ClaimTypes.Role, request.Role),
                    new Claim("UserId", request.UserId.ToString()),
                    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                };

                // Add role-specific claims for HMS
                if (request.PatientId.HasValue)
                {
                    claims.Add(new Claim("PatientId", request.PatientId.Value.ToString()));
                }

                if (request.DoctorId.HasValue)
                {
                    claims.Add(new Claim("DoctorId", request.DoctorId.Value.ToString()));
                }

                // Add additional HMS claims based on role
                switch (request.Role.ToLower())
                {
                    case "admin":
                        claims.Add(new Claim("Permissions", "FullAccess"));
                        claims.Add(new Claim("Department", "Administration"));
                        break;
                    case "doctor":
                        claims.Add(new Claim("Permissions", "MedicalAccess"));
                        claims.Add(new Claim("Specialty", request.Specialty ?? "GeneralPractice"));
                        break;
                    case "patient":
                        claims.Add(new Claim("Permissions", "PatientAccess"));
                        claims.Add(new Claim("MembershipLevel", "Standard"));
                        break;
                }

                var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");
                var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: expiry,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                _logger.LogInformation("Generated test JWT token for HMS BillingService - User: {UserId}, Role: {Role}", 
                    request.UserId, request.Role);

                return Ok(new
                {
                    token = tokenString,
                    expiresAt = expiry,
                    user = new
                    {
                        userId = request.UserId,
                        role = request.Role,
                        patientId = request.PatientId,
                        doctorId = request.DoctorId,
                        specialty = request.Specialty
                    },
                    tokenInfo = new
                    {
                        issuer = issuer,
                        audience = audience,
                        expiryMinutes = expiryMinutes
                    },
                    service = "HMS Billing Service",
                    message = $"Test JWT token generated for {request.Role} role"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating test JWT token for HMS BillingService");
                return BadRequest(new
                {
                    error = "Token generation failed",
                    message = ex.Message,
                    service = "HMS Billing Service"
                });
            }
        }

        /// <summary>
        /// Validate JWT token for HMS BillingService testing
        /// </summary>
        [HttpPost("validate-token")]
        public ActionResult<object> ValidateTestToken([FromBody] ValidateTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { error = "Token is required", service = "HMS Billing Service" });
                }

                var jwtSettings = _configuration.GetSection("Jwt");
                var secretKey = jwtSettings["SecretKey"] ?? "HMS_SuperSecretKey_ForDevelopment_2024_MustBe32CharsOrMore!";
                var issuer = jwtSettings["Issuer"] ?? "HMS.AuthService";
                var audience = jwtSettings["Audience"] ?? "HMS.Services";

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out SecurityToken validatedToken);
                
                var jwtToken = validatedToken as JwtSecurityToken;
                var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);

                _logger.LogInformation("JWT token validated successfully for HMS BillingService - User: {UserId}", 
                    claims.GetValueOrDefault(ClaimTypes.NameIdentifier));

                return Ok(new
                {
                    valid = true,
                    user = new
                    {
                        userId = claims.GetValueOrDefault(ClaimTypes.NameIdentifier),
                        role = claims.GetValueOrDefault(ClaimTypes.Role),
                        patientId = claims.GetValueOrDefault("PatientId"),
                        doctorId = claims.GetValueOrDefault("DoctorId"),
                        permissions = claims.GetValueOrDefault("Permissions")
                    },
                    tokenInfo = new
                    {
                        issuer = jwtToken?.Issuer,
                        audience = jwtToken?.Audiences?.FirstOrDefault(),
                        expiry = jwtToken?.ValidTo,
                        issuedAt = jwtToken?.IssuedAt
                    },
                    service = "HMS Billing Service",
                    message = "Token is valid and authenticated"
                });
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("Invalid JWT token provided to HMS BillingService: {Error}", ex.Message);
                return Unauthorized(new
                {
                    valid = false,
                    error = "Invalid token",
                    message = ex.Message,
                    service = "HMS Billing Service"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token for HMS BillingService");
                return StatusCode(500, new
                {
                    valid = false,
                    error = "Token validation failed",
                    message = ex.Message,
                    service = "HMS Billing Service"
                });
            }
        }

        /// <summary>
        /// Get service capabilities for HMS BillingService testing
        /// </summary>
        [HttpGet("service-info")]
        public ActionResult<object> GetServiceInfo()
        {
            try
            {
                return Ok(new
                {
                    service = "HMS Billing Service",
                    version = "1.0.0",
                    environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
                    capabilities = new
                    {
                        authentication = "JWT Bearer Token",
                        authorization = "Role-based (Admin, Doctor, Patient)",
                        paymentMethods = new[] { "Stripe", "Cash" },
                        factoryPattern = "Payment method factory for extensibility",
                        features = new[]
                        {
                            "Create Billing Records",
                            "Process Payments (Stripe & Cash)",
                            "Handle Refunds",
                            "Role-based Access Control",
                            "RabbitMQ Event Publishing",
                            "gRPC Inter-service Communication"
                        }
                    },
                    endpoints = new
                    {
                        generateToken = "/api/test/generate-token",
                        validateToken = "/api/test/validate-token",
                        billings = "/api/billings",
                        paymentMethods = "/api/billings/payment-methods",
                        processPayment = "/api/billings/{id}/process-payment",
                        refund = "/api/billings/{id}/refund"
                    },
                    testUsers = new[]
                    {
                        new { role = "Admin", userId = 1, permissions = "Full system access" },
                        new { role = "Doctor", userId = 2, permissions = "Medical records and billing access" },
                        new { role = "Patient", userId = 3, permissions = "Own billing records only" }
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HMS BillingService info");
                return StatusCode(500, new
                {
                    error = "Failed to get service info",
                    message = ex.Message,
                    service = "HMS Billing Service"
                });
            }
        }
    }

    public class GenerateTokenRequest
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin"; // Admin, Doctor, Patient
        public int? PatientId { get; set; }
        public int? DoctorId { get; set; }
        public string? Specialty { get; set; } // For Doctor role
    }

    public class ValidateTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}
