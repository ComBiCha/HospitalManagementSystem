using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Application.Services;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IAuthRepository _authRepository;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IAuthRepository authRepository, ILogger<UsersController> logger)
        {
            _authRepository = authRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get all users (Admin only)
        /// </summary>
        /// <returns>List of users</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserInfo>>> GetAllUsers()
        {
            try
            {
                var users = await _authRepository.GetAllUsersAsync();
                var userInfos = users.Select(u => new UserInfo
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role,
                    PatientId = u.PatientId,
                    DoctorId = u.DoctorId
                });

                return Ok(userInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User information</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<UserInfo>> GetUser(int id)
        {
            try
            {
                var user = await _authRepository.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
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
                _logger.LogError(ex, "Error getting user by ID: {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get users by role
        /// </summary>
        /// <param name="role">User role (Patient, Doctor, Admin)</param>
        /// <returns>List of users with specified role</returns>
        [HttpGet("role/{role}")]
        public async Task<ActionResult<IEnumerable<UserInfo>>> GetUsersByRole(string role)
        {
            try
            {
                var users = await _authRepository.GetUsersByRoleAsync(role);
                var userInfos = users.Select(u => new UserInfo
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role,
                    PatientId = u.PatientId,
                    DoctorId = u.DoctorId
                });

                return Ok(userInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role: {Role}", role);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update user information
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="request">Updated user data</param>
        /// <returns>Updated user information</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<UserInfo>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _authRepository.GetUserByIdAsync(id);
                if (user == null) return NotFound();

                // Map ALL fields from request to user entity
                user.Username = request.Username ?? user.Username;
                user.Email = request.Email ?? user.Email;
                user.FirstName = request.FirstName ?? user.FirstName;
                user.LastName = request.LastName ?? user.LastName;
                user.Role = request.Role ?? user.Role;
                user.PatientId = request.PatientId;
                user.DoctorId = request.DoctorId;
                user.IsActive = request.IsActive ?? user.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                await _authRepository.UpdateUserAsync(user);

                return Ok(new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    PatientId = user.PatientId,
                    DoctorId = user.DoctorId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deactivate user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var success = await _authRepository.DeleteUserAsync(id);
                if (!success)
                {
                    return NotFound($"User with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public int? PatientId { get; set; }
        public int? DoctorId { get; set; }
        public bool? IsActive { get; set; }
    }
}