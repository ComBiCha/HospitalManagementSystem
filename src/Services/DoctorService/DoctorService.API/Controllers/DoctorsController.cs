using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using DoctorService.API.Models;
using DoctorService.API.Repositories;

namespace DoctorService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorsController : ControllerBase
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly ILogger<DoctorsController> _logger;

        public DoctorsController(IDoctorRepository doctorRepository, ILogger<DoctorsController> logger)
        {
            _doctorRepository = doctorRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get all doctors
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Doctor>>> GetAllDoctors()
        {
            try
            {
                var doctors = await _doctorRepository.GetAllAsync();
                return Ok(doctors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all doctors");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get doctor by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Doctor>> GetDoctor(int id)
        {
            try
            {
                var doctor = await _doctorRepository.GetByIdAsync(id);
                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {id} not found");
                }

                return Ok(doctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctor by ID: {DoctorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get doctor by email
        /// </summary>
        [HttpGet("email/{email}")]
        public async Task<ActionResult<Doctor>> GetDoctorByEmail(string email)
        {
            try
            {
                var doctor = await _doctorRepository.GetByEmailAsync(email);
                if (doctor == null)
                {
                    return NotFound($"Doctor with email {email} not found");
                }

                return Ok(doctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctor by email: {Email}", email);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get doctors by specialty
        /// </summary>
        [HttpGet("specialty/{specialty}")]
        public async Task<ActionResult<IEnumerable<Doctor>>> GetDoctorsBySpecialty(string specialty)
        {
            try
            {
                var doctors = await _doctorRepository.GetBySpecialtyAsync(specialty);
                return Ok(doctors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctors by specialty: {Specialty}", specialty);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create new doctor
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Doctor>> CreateDoctor(CreateDoctorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // ✅ Check if email already exists
                var emailExists = await _doctorRepository.EmailExistsAsync(request.Email);
                if (emailExists)
                {
                    return BadRequest($"Doctor with email {request.Email} already exists");
                }

                var doctor = new Doctor
                {
                    Name = request.Name,
                    Specialty = request.Specialty,
                    Email = request.Email  // ✅ Set email
                };

                var createdDoctor = await _doctorRepository.CreateAsync(doctor);
                return CreatedAtAction(nameof(GetDoctor), new { id = createdDoctor.Id }, createdDoctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating doctor");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update doctor
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<Doctor>> UpdateDoctor(int id, UpdateDoctorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // ✅ Check if email exists for another doctor
                var existingDoctorWithEmail = await _doctorRepository.GetByEmailAsync(request.Email);
                if (existingDoctorWithEmail != null && existingDoctorWithEmail.Id != id)
                {
                    return BadRequest($"Email {request.Email} is already used by another doctor");
                }

                var doctor = new Doctor
                {
                    Id = id,
                    Name = request.Name,
                    Specialty = request.Specialty,
                    Email = request.Email  
                };

                var updatedDoctor = await _doctorRepository.UpdateAsync(doctor);
                if (updatedDoctor == null)
                {
                    return NotFound($"Doctor with ID {id} not found");
                }

                return Ok(updatedDoctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating doctor: {DoctorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete doctor
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            try
            {
                var success = await _doctorRepository.DeleteAsync(id);
                if (!success)
                {
                    return NotFound($"Doctor with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting doctor: {DoctorId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// Request model for creating doctor
    /// </summary>
    public class CreateDoctorRequest
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Specialty { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;  // ✅ Email field
    }

    /// <summary>
    /// Request model for updating doctor
    /// </summary>
    public class UpdateDoctorRequest
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required] 
        [StringLength(100)]
        public string Specialty { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;  // ✅ Email field
    }
}
