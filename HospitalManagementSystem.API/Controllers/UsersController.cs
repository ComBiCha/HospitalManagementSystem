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
        public async Task<ActionResult<UserInfo>> UpdateUser(int id, UpdateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingUser = await _authRepository.GetUserByIdAsync(id);
                if (existingUser == null)
                {
                    return NotFound($"User with ID {id} not found");
                }

                existingUser.Email = request.Email;
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.Role = request.Role;
                existingUser.PatientId = request.PatientId;
                existingUser.DoctorId = request.DoctorId;

                var updatedUser = await _authRepository.UpdateUserAsync(existingUser);
                if (updatedUser == null)
                {
                    return StatusCode(500, "Failed to update user");
                }

                var userInfo = new UserInfo
                {
                    Id = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    Role = updatedUser.Role,
                    PatientId = updatedUser.PatientId,
                    DoctorId = updatedUser.DoctorId
                };

                return Ok(userInfo);
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
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        public string Role { get; set; } = string.Empty;
        
        public int? PatientId { get; set; }
        public int? DoctorId { get; set; }
    }
}