using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using AuthenticationService.API.Models;
using AuthenticationService.API.Services;
using AuthenticationService.API.Repositories;

namespace AuthenticationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _authRepository;
        private readonly IJwtTokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthRepository authRepository, 
            IJwtTokenService tokenService, 
            ILogger<AuthController> logger)
        {
            _authRepository = authRepository;
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// User login
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>JWT token and user info</returns>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Username}", request.Username);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _authRepository.GetUserByUsernameAsync(request.Username);
                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid login attempt for user: {Username}", request.Username);
                    return Unauthorized("Invalid username or password");
                }

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                await _authRepository.UpdateUserAsync(user);

                // Generate tokens
                var accessToken = _tokenService.GenerateToken(user);
                var refreshTokenString = _tokenService.GenerateRefreshToken();

                // Create and store refresh token
                var refreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    UserId = user.Id,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                };

                await _authRepository.CreateRefreshTokenAsync(refreshToken);

                var response = new AuthResponse
                {
                    Token = accessToken,
                    RefreshToken = refreshTokenString,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId,
                        DoctorId = user.DoctorId
                    }
                };

                _logger.LogInformation("Successful login for user: {Username}", request.Username);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// User registration
        /// </summary>
        /// <param name="request">Registration data</param>
        /// <returns>JWT token and user info</returns>
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for user: {Username}", request.Username);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if username or email already exists
                var userExists = await _authRepository.UserExistsAsync(request.Username, request.Email);
                if (userExists)
                {
                    return BadRequest("Username or email already exists");
                }

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = passwordHash,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Role = request.Role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdUser = await _authRepository.CreateUserAsync(user);

                // Generate tokens
                var accessToken = _tokenService.GenerateToken(createdUser);
                var refreshTokenString = _tokenService.GenerateRefreshToken();

                // Create and store refresh token
                var refreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    UserId = createdUser.Id,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                };

                await _authRepository.CreateRefreshTokenAsync(refreshToken);

                var response = new AuthResponse
                {
                    Token = accessToken,
                    RefreshToken = refreshTokenString,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    User = new UserInfo
                    {
                        Id = createdUser.Id,
                        Username = createdUser.Username,
                        Email = createdUser.Email,
                        FirstName = createdUser.FirstName,
                        LastName = createdUser.LastName,
                        Role = createdUser.Role,
                        PatientId = createdUser.PatientId,
                        DoctorId = createdUser.DoctorId
                    }
                };

                _logger.LogInformation("Successful registration for user: {Username}", request.Username);
                return CreatedAtAction(nameof(GetProfile), new { }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user: {Username}", request.Username);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        /// <param name="request">Refresh token request</param>
        /// <returns>New JWT tokens</returns>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> RefreshToken(RefreshTokenRequest request)
        {
            try
            {
                _logger.LogInformation("Refresh token request received");

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var refreshToken = await _authRepository.GetRefreshTokenAsync(request.RefreshToken);
                if (refreshToken == null || !refreshToken.IsActive)
                {
                    _logger.LogWarning("Invalid or expired refresh token");
                    return Unauthorized("Invalid refresh token");
                }

                var user = await _authRepository.GetUserByIdAsync(refreshToken.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for refresh token");
                    return Unauthorized("User not found");
                }

                // Revoke old refresh token
                await _authRepository.RevokeRefreshTokenAsync(request.RefreshToken);

                // Generate new tokens
                var newAccessToken = _tokenService.GenerateToken(user);
                var newRefreshTokenString = _tokenService.GenerateRefreshToken();

                // Create and store new refresh token
                var newRefreshToken = new RefreshToken
                {
                    Token = newRefreshTokenString,
                    UserId = user.Id,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                };

                await _authRepository.CreateRefreshTokenAsync(newRefreshToken);

                var response = new AuthResponse
                {
                    Token = newAccessToken,
                    RefreshToken = newRefreshTokenString,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId,
                        DoctorId = user.DoctorId
                    }
                };

                _logger.LogInformation("Successful token refresh for user: {Username}", user.Username);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Logout user by revoking refresh token
        /// </summary>
        /// <param name="request">Logout request</param>
        /// <returns>Success status</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest request)
        {
            try
            {
                _logger.LogInformation("Logout request received");

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _authRepository.RevokeRefreshTokenAsync(request.RefreshToken);
                if (!success)
                {
                    _logger.LogWarning("Failed to revoke refresh token during logout");
                    return BadRequest("Invalid refresh token");
                }

                _logger.LogInformation("Successful logout");
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Validate JWT token
        /// </summary>
        /// <param name="request">Token validation request</param>
        /// <returns>Token validation result</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateToken(ValidateTokenRequest request)
        {
            try
            {
                var principal = _tokenService.ValidateToken(request.Token);
                if (principal == null)
                {
                    return Unauthorized("Invalid token");
                }

                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized("Invalid user ID in token");
                }

                // Verify user still exists and is active
                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                var username = principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                return Ok(new
                {
                    IsValid = true,
                    UserId = userId,
                    Username = username,
                    Role = role,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId,
                        DoctorId = user.DoctorId
                    },
                    Claims = principal.Claims.Select(c => new { c.Type, c.Value })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return Unauthorized("Invalid token");
            }
        }

        /// <summary>
        /// Get current user profile
        /// </summary>
        /// <returns>User profile information</returns>
        [HttpGet("profile")]
        public async Task<ActionResult<UserInfo>> GetProfile()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("No authorization header");
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var principal = _tokenService.ValidateToken(token);
                
                if (principal == null)
                {
                    return Unauthorized("Invalid token");
                }

                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized("Invalid user ID in token");
                }

                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var userInfo = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    PatientId = user.PatientId,
                    DoctorId = user.DoctorId
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Revoke all refresh tokens for a user (useful for security incidents)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        [HttpPost("revoke-all/{userId}")]
        public async Task<IActionResult> RevokeAllUserTokens(int userId)
        {
            try
            {
                _logger.LogInformation("Revoking all tokens for user: {UserId}", userId);

                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var success = await _authRepository.RevokeAllUserRefreshTokensAsync(userId);
                if (!success)
                {
                    return BadRequest("Failed to revoke tokens");
                }

                _logger.LogInformation("Successfully revoked all tokens for user: {UserId}", userId);
                return Ok(new { message = "All tokens revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user: {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Additional DTOs for new endpoints
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ValidateTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}