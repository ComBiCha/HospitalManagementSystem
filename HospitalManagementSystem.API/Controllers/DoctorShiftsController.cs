using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Doctor")]
    public class DoctorShiftsController : ControllerBase
    {
        private readonly IDoctorShiftRepository _shiftRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ILogger<DoctorShiftsController> _logger;

        public DoctorShiftsController(
            IDoctorShiftRepository shiftRepository,
            IDoctorRepository doctorRepository,
            ILogger<DoctorShiftsController> logger)
        {
            _shiftRepository = shiftRepository;
            _doctorRepository = doctorRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get all doctor shifts
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DoctorShiftResponseDto>>> GetAllShifts()
        {
            try
            {
                var shifts = await _shiftRepository.GetAllAsync();
                var result = shifts.Select(s => MapToDto(s)).ToList();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all shifts");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get shift by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<DoctorShiftResponseDto>> GetShift(int id)
        {
            try
            {
                var shift = await _shiftRepository.GetByIdAsync(id);
                if (shift == null)
                {
                    return NotFound($"Shift with ID {id} not found");
                }

                return Ok(MapToDto(shift));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shift {ShiftId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get shifts by doctor ID
        /// </summary>
        [HttpGet("doctor/{doctorId}")]
        public async Task<ActionResult<IEnumerable<DoctorShiftResponseDto>>> GetShiftsByDoctor(int doctorId)
        {
            try
            {
                var shifts = await _shiftRepository.GetByDoctorIdAsync(doctorId);
                var result = shifts.Select(s => MapToDto(s)).ToList();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shifts for doctor {DoctorId}", doctorId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new doctor shift
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DoctorShiftResponseDto>> CreateShift(CreateDoctorShiftRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate doctor exists
                var doctor = await _doctorRepository.GetByIdAsync(request.DoctorId);
                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {request.DoctorId} not found");
                }

                // Validate time range
                if (request.StartTime >= request.EndTime)
                {
                    return BadRequest("Start time must be before end time");
                }

                var shift = new DoctorShift
                {
                    DoctorId = request.DoctorId,
                    DayOfWeek = request.DayOfWeek,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    IsActive = true
                };

                var created = await _shiftRepository.CreateAsync(shift);

                _logger.LogInformation("Created shift {ShiftId} for doctor {DoctorId}", created.Id, request.DoctorId);

                return CreatedAtAction(nameof(GetShift), new { id = created.Id }, MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shift");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update a doctor shift
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<DoctorShiftResponseDto>> UpdateShift(int id, UpdateDoctorShiftRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existing = await _shiftRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound($"Shift with ID {id} not found");
                }

                // Validate time range
                if (request.StartTime >= request.EndTime)
                {
                    return BadRequest("Start time must be before end time");
                }

                var shift = new DoctorShift
                {
                    Id = id,
                    DoctorId = existing.DoctorId,
                    DayOfWeek = request.DayOfWeek,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    IsActive = request.IsActive,
                    CreatedAt = existing.CreatedAt
                };

                var updated = await _shiftRepository.UpdateAsync(shift);
                if (updated == null)
                {
                    return NotFound($"Shift with ID {id} not found");
                }

                _logger.LogInformation("Updated shift {ShiftId}", id);

                return Ok(MapToDto(updated));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shift {ShiftId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete a doctor shift (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShift(int id)
        {
            try
            {
                var deleted = await _shiftRepository.DeleteAsync(id);
                if (!deleted)
                {
                    return NotFound($"Shift with ID {id} not found");
                }

                _logger.LogInformation("Deleted shift {ShiftId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift {ShiftId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private DoctorShiftResponseDto MapToDto(DoctorShift shift)
        {
            return new DoctorShiftResponseDto
            {
                Id = shift.Id,
                DoctorId = shift.DoctorId,
                DoctorName = shift.Doctor?.Name ?? "",
                DayOfWeek = shift.DayOfWeek,
                DayOfWeekName = shift.DayOfWeek.ToString(),
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                StartTimeDisplay = shift.StartTime.ToString(@"hh\:mm"),
                EndTimeDisplay = shift.EndTime.ToString(@"hh\:mm"),
                IsActive = shift.IsActive,
                CreatedAt = shift.CreatedAt,
                UpdatedAt = shift.UpdatedAt
            };
        }
    }

    // DTOs
    public class CreateDoctorShiftRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }
    }

    public class UpdateDoctorShiftRequest
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class DoctorShiftResponseDto
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public DayOfWeek DayOfWeek { get; set; }
        public string DayOfWeekName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string StartTimeDisplay { get; set; } = string.Empty;
        public string EndTimeDisplay { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}