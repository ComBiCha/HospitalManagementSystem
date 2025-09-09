using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.API.Grpc;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Application.Services;
using Google.Protobuf.WellKnownTypes;

namespace HospitalManagementSystem.API.Services
{
    public class AuthGrpcService : AuthService.AuthServiceBase
    {
        private readonly HospitalDbContext _context;
        private readonly IJwtTokenService _tokenService;
        private readonly ILogger<AuthGrpcService> _logger;

        public AuthGrpcService(HospitalDbContext context, IJwtTokenService tokenService, ILogger<AuthGrpcService> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        public override async Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC ValidateToken request received");

                var principal = _tokenService.ValidateToken(request.Token);
                if (principal == null)
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Error = "Invalid token"
                    };
                }

                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Error = "Invalid user ID in token"
                    };
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Error = "User not found or inactive"
                    };
                }

                return new ValidateTokenResponse
                {
                    IsValid = true,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId ?? 0,
                        DoctorId = user.DoctorId ?? 0,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token via gRPC");
                return new ValidateTokenResponse
                {
                    IsValid = false,
                    Error = "Token validation failed"
                };
            }
        }

        public override async Task<GetUserByIdResponse> GetUserById(GetUserByIdRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetUserById request for ID: {UserId}", request.UserId);

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null || !user.IsActive)
                {
                    return new GetUserByIdResponse
                    {
                        Error = "User not found"
                    };
                }

                return new GetUserByIdResponse
                {
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId ?? 0,
                        DoctorId = user.DoctorId ?? 0,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID via gRPC: {UserId}", request.UserId);
                return new GetUserByIdResponse
                {
                    Error = "Failed to get user"
                };
            }
        }

        public override async Task<GetUserByUsernameResponse> GetUserByUsername(GetUserByUsernameRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetUserByUsername request for: {Username}", request.Username);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

                if (user == null)
                {
                    return new GetUserByUsernameResponse
                    {
                        Error = "User not found"
                    };
                }

                return new GetUserByUsernameResponse
                {
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Role = user.Role,
                        PatientId = user.PatientId ?? 0,
                        DoctorId = user.DoctorId ?? 0,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username via gRPC: {Username}", request.Username);
                return new GetUserByUsernameResponse
                {
                    Error = "Failed to get user"
                };
            }
        }
    }
}