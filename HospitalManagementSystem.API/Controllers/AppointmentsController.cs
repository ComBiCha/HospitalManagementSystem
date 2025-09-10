using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Domain.Events;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(
            IAppointmentRepository appointmentRepository,
            IPatientRepository patientRepository,
            IDoctorRepository doctorRepository,
            IRabbitMQService rabbitMQService, 
            ILogger<AppointmentsController> logger)
        {
            _appointmentRepository = appointmentRepository;
            _patientRepository = patientRepository;
            _doctorRepository = doctorRepository;
            _rabbitMQService = rabbitMQService; 
            _logger = logger;
        }

        /// <summary>
        /// Get all appointments (Admin/Doctor access)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetAllAppointments()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();
                
                _logger.LogInformation("User {UserId} with role {Role} getting all appointments via REST API", 
                    currentUserId, currentUserRole);

                var appointments = await _appointmentRepository.GetAllAsync();
                var appointmentDtos = new List<AppointmentDto>();

                foreach (var appointment in appointments)
                {
                    var appointmentDto = await MapToAppointmentDto(appointment);
                    appointmentDtos.Add(appointmentDto);
                }

                return Ok(appointmentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all appointments");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get appointment by ID with authorization
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<AppointmentDto>> GetAppointment(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                _logger.LogInformation("User {UserId} getting appointment {AppointmentId} via REST API", 
                    currentUserId, id);
                
                var appointment = await _appointmentRepository.GetByIdAsync(id);
                if (appointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                if (!await CanAccessAppointment(appointment, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to access this appointment");
                }

                var appointmentDto = await MapToAppointmentDto(appointment);
                return Ok(appointmentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointment with ID: {AppointmentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get appointments by patient ID with authorization
        /// </summary>
        [HttpGet("patient/{patientId}")]
        public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetAppointmentsByPatient(int patientId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId();
                    if (userPatientId != patientId)
                    {
                        return Forbid("You can only view your own appointments");
                    }
                }

                _logger.LogInformation("User {UserId} getting appointments for patient: {PatientId} via REST API", 
                    currentUserId, patientId);
                
                var appointments = await _appointmentRepository.GetByPatientIdAsync(patientId);
                var appointmentDtos = new List<AppointmentDto>();

                foreach (var appointment in appointments)
                {
                    var appointmentDto = await MapToAppointmentDto(appointment);
                    appointmentDtos.Add(appointmentDto);
                }

                return Ok(appointmentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for patient: {PatientId}", patientId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get appointments by doctor ID with authorization
        /// </summary>
        [HttpGet("doctor/{doctorId}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetAppointmentsByDoctor(int doctorId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (currentUserRole == "Doctor")
                {
                    var userDoctorId = GetCurrentUserDoctorId();
                    if (userDoctorId != doctorId)
                    {
                        return Forbid("You can only view your own appointments");
                    }
                }

                _logger.LogInformation("User {UserId} getting appointments for doctor: {DoctorId} via REST API", 
                    currentUserId, doctorId);
                
                var appointments = await _appointmentRepository.GetByDoctorIdAsync(doctorId);
                var appointmentDtos = new List<AppointmentDto>();

                foreach (var appointment in appointments)
                {
                    var appointmentDto = await MapToAppointmentDto(appointment);
                    appointmentDtos.Add(appointmentDto);
                }

                return Ok(appointmentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for doctor: {DoctorId}", doctorId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new appointment with JWT authorization and RabbitMQ event
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Patient,Doctor")]
        public async Task<ActionResult<AppointmentDto>> CreateAppointment(CreateAppointmentRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId();
                    if (userPatientId != request.PatientId)
                    {
                        return Forbid("You can only create appointments for yourself");
                    }
                }

                _logger.LogInformation("User {UserId} creating appointment via REST API: PatientId={PatientId}, DoctorId={DoctorId}, Date={Date}", 
                    currentUserId, request.PatientId, request.DoctorId, request.Date);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for CreateAppointment: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                // Convert DateTime to UTC for PostgreSQL compatibility
                var utcDate = request.Date.Kind == DateTimeKind.Utc 
                    ? request.Date 
                    : DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);

                // Additional validation
                if (utcDate <= DateTime.UtcNow)
                {
                    return BadRequest("Appointment date must be in the future");
                }

                // Validate patient exists
                var patient = await _patientRepository.GetPatientByIdAsync(request.PatientId);
                if (patient == null)
                {
                    return NotFound($"Patient with ID {request.PatientId} not found");
                }

                // Validate doctor exists
                var doctor = await _doctorRepository.GetByIdAsync(request.DoctorId);
                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {request.DoctorId} not found");
                }

                // Check for conflicting appointments
                var hasConflict = await _appointmentRepository.HasConflictingAppointmentAsync(request.DoctorId, utcDate);
                if (hasConflict)
                {
                    return Conflict("Doctor already has an appointment at this time");
                }

                var appointment = new Appointment
                {
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    Date = utcDate,
                    Status = string.IsNullOrEmpty(request.Status) ? "Scheduled" : request.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdAppointment = await _appointmentRepository.CreateAsync(appointment);

                await _rabbitMQService.PublishAppointmentCreatedAsync(new AppointmentCreatedEvent
                {
                    AppointmentId = createdAppointment.Id,
                    PatientId = createdAppointment.PatientId,
                    PatientName = patient.Name,
                    DoctorId = createdAppointment.DoctorId,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty,
                    Date = createdAppointment.Date,
                    Status = createdAppointment.Status,
                    CreatedAt = createdAppointment.CreatedAt,
                    CreatedByUserId = currentUserId, 
                    CreatedByRole = currentUserRole   
                });

                var appointmentDto = await MapToAppointmentDto(createdAppointment);

                _logger.LogInformation("Appointment {AppointmentId} created by user {UserId} with role {Role}", 
                    createdAppointment.Id, currentUserId, currentUserRole);

                return CreatedAtAction(nameof(GetAppointment), new { id = createdAppointment.Id }, appointmentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update an existing appointment with JWT authorization and RabbitMQ event
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<ActionResult<AppointmentDto>> UpdateAppointment(int id, UpdateAppointmentRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                _logger.LogInformation("User {UserId} updating appointment with ID: {AppointmentId} via REST API", 
                    currentUserId, id);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingAppointment = await _appointmentRepository.GetByIdAsync(id);
                if (existingAppointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                if (!await CanAccessAppointment(existingAppointment, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to update this appointment");
                }

                // Convert DateTime to UTC for PostgreSQL compatibility
                var utcDate = request.Date.Kind == DateTimeKind.Utc 
                    ? request.Date 
                    : DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);

                // Validate patient exists
                var patient = await _patientRepository.GetPatientByIdAsync(request.PatientId);
                if (patient == null)
                {
                    return NotFound($"Patient with ID {request.PatientId} not found");
                }

                // Validate doctor exists
                var doctor = await _doctorRepository.GetByIdAsync(request.DoctorId);
                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {request.DoctorId} not found");
                }

                // Check for conflicting appointments (excluding current appointment)
                var hasConflict = await _appointmentRepository.HasConflictingAppointmentAsync(request.DoctorId, utcDate, id);
                if (hasConflict)
                {
                    return Conflict("Doctor already has an appointment at this time");
                }

                var appointment = new Appointment
                {
                    Id = id,
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    Date = utcDate,
                    Status = request.Status,
                    CreatedAt = existingAppointment.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };

                var updatedAppointment = await _appointmentRepository.UpdateAsync(appointment);
                if (updatedAppointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                await _rabbitMQService.PublishAppointmentUpdatedAsync(new AppointmentUpdatedEvent
                {
                    AppointmentId = updatedAppointment.Id,
                    PatientId = updatedAppointment.PatientId,
                    PatientName = patient.Name,
                    DoctorId = updatedAppointment.DoctorId,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty,
                    Date = updatedAppointment.Date,
                    Status = updatedAppointment.Status,
                    UpdatedAt = updatedAppointment.UpdatedAt,
                    UpdatedByUserId = currentUserId, 
                    UpdatedByRole = currentUserRole  
                });

                var appointmentDto = await MapToAppointmentDto(updatedAppointment);

                _logger.LogInformation("Appointment {AppointmentId} updated by user {UserId} with role {Role}", 
                    id, currentUserId, currentUserRole);

                return Ok(appointmentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment with ID: {AppointmentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete an appointment with JWT authorization and RabbitMQ event
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                _logger.LogInformation("User {UserId} deleting appointment with ID: {AppointmentId} via REST API", 
                    currentUserId, id);

                var appointment = await _appointmentRepository.GetByIdAsync(id);
                if (appointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }


                // ✅ Role-based access control
                if (!await CanAccessAppointment(appointment, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to cancel this appointment");
                }

                var patient = await _patientRepository.GetPatientByIdAsync(appointment.PatientId);
                if (patient == null)
                {
                    return NotFound($"Patient with ID {appointment.PatientId} not found");
                }

                // Validate doctor exists
                var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {appointment.DoctorId} not found");
                }

                var deleted = await _appointmentRepository.DeleteAsync(id);
                if (!deleted)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                await _rabbitMQService.PublishAppointmentCancelledAsync(new AppointmentCancelledEvent
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    PatientName = patient?.Name ?? "",
                    DoctorId = appointment.DoctorId,
                    DoctorName = doctor?.Name ?? "",
                    DoctorSpecialty = doctor?.Specialty ?? "",
                    Date = appointment.Date,
                    Status = "Cancelled",
                    CancelledAt = DateTime.UtcNow,
                    CancelledByUserId = currentUserId, 
                    CancelledByRole = currentUserRole   
                });

                _logger.LogInformation("Appointment {AppointmentId} cancelled by user {UserId} with role {Role}", 
                    id, currentUserId, currentUserRole);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting appointment with ID: {AppointmentId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // ✅ Helper methods for JWT claims extraction
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private int? GetCurrentUserPatientId()
        {
            var patientIdClaim = User.FindFirst("PatientId")?.Value;
            return int.TryParse(patientIdClaim, out int patientId) ? patientId : null;
        }

        private int? GetCurrentUserDoctorId()
        {
            var doctorIdClaim = User.FindFirst("DoctorId")?.Value;
            return int.TryParse(doctorIdClaim, out int doctorId) ? doctorId : null;
        }

        // ✅ Authorization helper method
        // ✅ Fix async method warning - line 485
        private async Task<bool> CanAccessAppointment(Appointment appointment, int userId, string userRole)
        {
            await Task.CompletedTask;
            
            switch (userRole)
            {
                case "Admin":
                    return true;

                case "Doctor":
                    var userDoctorId = GetCurrentUserDoctorId();
                    return userDoctorId == appointment.DoctorId;

                case "Patient":
                    var userPatientId = GetCurrentUserPatientId();
                    return userPatientId == appointment.PatientId;

                default:
                    return false;
            }
        }

        // ✅ Mapping helper method
        private async Task<AppointmentDto> MapToAppointmentDto(Appointment appointment)
        {
            var patient = await _patientRepository.GetPatientByIdAsync(appointment.PatientId);
            var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);

            return new AppointmentDto
            {
                Id = appointment.Id,
                PatientId = appointment.PatientId,
                DoctorId = appointment.DoctorId,
                Date = appointment.Date,
                Status = appointment.Status,
                PatientName = patient?.Name ?? "Unknown Patient",
                DoctorName = doctor?.Name ?? "Unknown Doctor",
                DoctorSpecialty = doctor?.Specialty ?? "Unknown Specialty"
            };
        }
    }

    // ✅ DTOs remain the same as your current implementation
    public class CreateAppointmentRequest
    {
        [Required(ErrorMessage = "Patient ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Patient ID must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Doctor ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Doctor ID must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Appointment date is required")]
        public DateTime Date { get; set; }

        [StringLength(50, ErrorMessage = "Status cannot be longer than 50 characters")]
        public string Status { get; set; } = "Scheduled";
    }

    public class UpdateAppointmentRequest
    {
        [Required(ErrorMessage = "Patient ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Patient ID must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Doctor ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Doctor ID must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Appointment date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [StringLength(50, ErrorMessage = "Status cannot be longer than 50 characters")]
        public string Status { get; set; } = string.Empty;
    }

    public class AppointmentDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialty { get; set; } = string.Empty;
    }
}
