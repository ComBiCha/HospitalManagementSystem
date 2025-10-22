using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Application.DTOs;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentBookingController : ControllerBase
    {
        private readonly AppointmentApplicationService _appointmentService;
        private readonly ILogger<AppointmentBookingController> _logger;

        public AppointmentBookingController(
            AppointmentApplicationService appointmentService,
            ILogger<AppointmentBookingController> logger)
        {
            _appointmentService = appointmentService;
            _logger = logger;
        }

        [HttpGet("specialties")]
        public async Task<ActionResult<IEnumerable<string>>> GetSpecialties()
        {
            try
            {
                var specialties = await _appointmentService.GetSpecialtiesAsync();
                return Ok(specialties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting specialties");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("doctors-by-specialty")]
        public async Task<ActionResult<IEnumerable<AvailableDoctorDto>>> GetDoctorsBySpecialty([FromQuery] string specialty)
        {
            try
            {
                var doctors = await _appointmentService.GetDoctorsBySpecialtyAsync(specialty);
                return Ok(doctors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctors for specialty {Specialty}", specialty);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("available-slots")]
        public async Task<ActionResult<IEnumerable<TimeSlotDto>>> GetAvailableTimeSlots([FromQuery] DateTime date)
        {
            try
            {
                var timeSlots = await _appointmentService.GetAvailableTimeSlotsAsync(date);
                return Ok(timeSlots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available time slots");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("available-doctors")]
        public async Task<ActionResult<IEnumerable<AvailableDoctorDto>>> GetAvailableDoctors([FromQuery] DateTime appointmentDate, [FromQuery] string specialty)
        {
            try
            {
                var availableDoctors = await _appointmentService.GetAvailableDoctorsAsync(appointmentDate, specialty);
                return Ok(availableDoctors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available doctors");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("doctor-schedule/{doctorId}")]
        public async Task<ActionResult<IEnumerable<DoctorShiftDto>>> GetDoctorSchedule(int doctorId)
        {
            try
            {
                var schedule = await _appointmentService.GetDoctorScheduleAsync(doctorId);
                return Ok(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctor schedule");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("doctors/{doctorId}/available-dates")]
        public async Task<ActionResult<IEnumerable<DateTime>>> GetDoctorAvailableDates(int doctorId)
        {
            try
            {
                var availableDates = await _appointmentService.GetDoctorAvailableDatesAsync(doctorId);
                return Ok(availableDates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available dates for doctor {DoctorId}", doctorId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("doctors/{doctorId}/available-slots")]
        public async Task<ActionResult<IEnumerable<TimeSlotDto>>> GetDoctorAvailableSlots(int doctorId, [FromQuery] DateTime date)
        {
            try
            {
                var availableSlots = await _appointmentService.GetDoctorAvailableSlotsAsync(doctorId, date);
                return Ok(availableSlots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available slots for doctor {DoctorId} on {Date}", doctorId, date);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}