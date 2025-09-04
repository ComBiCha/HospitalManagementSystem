using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppointmentService.API.Models;

namespace AppointmentService.API.Services
{
    /// <summary>
    /// JWT Token Service implementation for HMS Authentication
    /// Follows Clean Architecture principles and SOLID design patterns
    /// </summary>
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public JwtTokenService(
            IOptions<JwtSettings> jwtSettings,
            HttpClient httpClient,
            ILogger<JwtTokenService> logger,
            IConfiguration configuration)
        {
            _jwtSettings = jwtSettings.Value;
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        /// <summary>
        /// Validates JWT token following HMS security standards
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("JWT token validation failed: Token is null or empty");
                    return false;
                }

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireExpirationTime = true
                };

                var principal = await Task.Run(() =>
                {
                    try
                    {
                        return _tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "JWT token validation failed during token parsing");
                        return null;
                    }
                });

                if (principal != null)
                {
                    _logger.LogDebug("JWT token validated successfully for HMS Appointment Service");
                    return true;
                }

                _logger.LogWarning("JWT token validation failed: Invalid token structure");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during JWT token validation for HMS Appointment Service");
                return false;
            }
        }

        /// <summary>
        /// Extracts claims from JWT token for HMS role-based authorization
        /// </summary>
        public async Task<ClaimsPrincipal?> GetClaimsFromTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Cannot extract claims: Token is null or empty");
                    return null;
                }

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireExpirationTime = true
                };

                var principal = await Task.Run(() =>
                {
                    try
                    {
                        return _tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract claims from JWT token");
                        return null;
                    }
                });

                if (principal != null)
                {
                    _logger.LogDebug("Successfully extracted claims from JWT token for HMS service");
                    return principal;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while extracting claims from JWT token");
                return null;
            }
        }

        /// <summary>
        /// Generates JWT token for HMS service-to-service communication
        /// </summary>
        public async Task<string> GenerateTokenAsync(int userId, string role, Dictionary<string, string>? additionalClaims = null)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new(ClaimTypes.Role, role),
                    new("service", "AppointmentService"),
                    new("iss", _jwtSettings.Issuer),
                    new("aud", _jwtSettings.Audience),
                    new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                // Add additional claims if provided
                if (additionalClaims != null)
                {
                    foreach (var claim in additionalClaims)
                    {
                        claims.Add(new Claim(claim.Key, claim.Value));
                    }
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                    SigningCredentials = credentials,
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience
                };

                var token = await Task.Run(() => _tokenHandler.CreateToken(tokenDescriptor));
                var tokenString = _tokenHandler.WriteToken(token);

                _logger.LogDebug("JWT token generated successfully for user {UserId} with role {Role} in HMS", userId, role);
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate JWT token for user {UserId} with role {Role}", userId, role);
                throw new InvalidOperationException("Failed to generate JWT token for HMS service", ex);
            }
        }

        /// <summary>
        /// Refreshes JWT token for HMS continuous authentication
        /// </summary>
        public async Task<string?> RefreshTokenAsync(string expiredToken)
        {
            try
            {
                if (string.IsNullOrEmpty(expiredToken))
                {
                    _logger.LogWarning("Cannot refresh token: Expired token is null or empty");
                    return null;
                }

                // Validate token without lifetime validation to extract claims
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = false, // Don't validate lifetime for refresh
                    ClockSkew = TimeSpan.Zero,
                    RequireExpirationTime = true
                };

                var principal = await Task.Run(() =>
                {
                    try
                    {
                        return _tokenHandler.ValidateToken(expiredToken, tokenValidationParameters, out var validatedToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to validate expired token for refresh");
                        return null;
                    }
                });

                if (principal == null)
                {
                    _logger.LogWarning("Cannot refresh token: Invalid token structure");
                    return null;
                }

                // Extract user information from expired token
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(roleClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("Cannot refresh token: Missing or invalid user claims");
                    return null;
                }

                // Extract additional claims for preservation
                var additionalClaims = principal.Claims
                    .Where(c => c.Type != ClaimTypes.NameIdentifier && 
                               c.Type != ClaimTypes.Role && 
                               c.Type != "iss" && 
                               c.Type != "aud" && 
                               c.Type != "iat" && 
                               c.Type != JwtRegisteredClaimNames.Jti &&
                               c.Type != JwtRegisteredClaimNames.Exp &&
                               c.Type != JwtRegisteredClaimNames.Nbf)
                    .ToDictionary(c => c.Type, c => c.Value);

                // Generate new token with preserved claims
                var newToken = await GenerateTokenAsync(userId, roleClaim, additionalClaims);
                
                _logger.LogDebug("JWT token refreshed successfully for user {UserId} in HMS", userId);
                return newToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during JWT token refresh for HMS service");
                return null;
            }
        }

        /// <summary>
        /// Validates JWT token with Auth Service for HMS inter-service communication
        /// </summary>
        public async Task<bool> ValidateTokenWithAuthServiceAsync(string token)
        {
            try
            {
                var authServiceUrl = _configuration["Services:AuthService:BaseUrl"];
                if (string.IsNullOrEmpty(authServiceUrl))
                {
                    _logger.LogWarning("Auth Service URL not configured, falling back to local validation");
                    return await ValidateTokenAsync(token);
                }

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await _httpClient.GetAsync($"{authServiceUrl}/api/auth/validate");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("JWT token validated successfully with HMS Auth Service");
                    return true;
                }

                _logger.LogWarning("JWT token validation failed with HMS Auth Service: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to connect to HMS Auth Service, falling back to local validation");
                return await ValidateTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Auth Service token validation");
                return false;
            }
        }
    }
}