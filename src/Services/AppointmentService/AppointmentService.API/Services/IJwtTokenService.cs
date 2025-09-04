using System.Security.Claims;

namespace AppointmentService.API.Services
{
    /// <summary>
    /// JWT Token Service interface for HMS Authentication
    /// Follows Clean Architecture principles for authentication abstraction
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Validates JWT token and returns whether it's valid
        /// </summary>
        /// <param name="token">JWT token string</param>
        /// <returns>True if token is valid, false otherwise</returns>
        Task<bool> ValidateTokenAsync(string token);

        /// <summary>
        /// Extracts claims from JWT token
        /// </summary>
        /// <param name="token">JWT token string</param>
        /// <returns>Claims principal if valid, null otherwise</returns>
        Task<ClaimsPrincipal?> GetClaimsFromTokenAsync(string token);

        /// <summary>
        /// Generates JWT token for service-to-service communication
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="role">User role</param>
        /// <param name="additionalClaims">Additional claims to include</param>
        /// <returns>JWT token string</returns>
        Task<string> GenerateTokenAsync(int userId, string role, Dictionary<string, string>? additionalClaims = null);

        /// <summary>
        /// Refreshes JWT token
        /// </summary>
        /// <param name="expiredToken">Expired JWT token</param>
        /// <returns>New JWT token if refresh is successful</returns>
        Task<string?> RefreshTokenAsync(string expiredToken);
    }
}