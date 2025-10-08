using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentBookingController : ControllerBase
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IDoctorShiftRepository _doctorShiftRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly HospitalDbContext _context;
        private readonly ILogger<AppointmentBookingController> _logger;

        public AppointmentBookingController(
            IDoctorRepository doctorRepository,
            IDoctorShiftRepository doctorShiftRepository,
            IAppointmentRepository appointmentRepository,
            HospitalDbContext context,
            ILogger<AppointmentBookingController> logger)
        {
            _doctorRepository = doctorRepository;
            _doctorShiftRepository = doctorShiftRepository;
            _appointmentRepository = appointmentRepository;
            _context = context;
            _logger = logger;
        }

        [HttpGet("specialties")]
        public async Task<ActionResult<IEnumerable<string>>> GetSpecialties()
        {
            try
            {
                var doctors = await _doctorRepository.GetAllAsync();
                var specialties = doctors
                    .Where(d => d.Status.HasFlag(DoctorStatus.Active))
                    .Select(d => d.Specialty)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                return Ok(specialties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting specialties");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("available-slots")]
        public async Task<ActionResult<IEnumerable<TimeSlotDto>>> GetAvailableTimeSlots(
            [FromQuery] DateTime date)
        {
            try
            {
                var timeSlots = new List<TimeSlotDto>();
                var dayOfWeek = date.DayOfWeek;

                for (int hour = 8; hour <= 16; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 30)
                    {
                        var slotTime = new TimeSpan(hour, minute, 0);
                        var slotDateTime = date.Date.Add(slotTime);

                        // Check if any doctor is working at this time
                        var hasWorkingDoctor = await _context.DoctorShifts
                            .AnyAsync(s => s.DayOfWeek == dayOfWeek 
                                && s.StartTime <= slotTime 
                                && s.EndTime >= slotTime 
                                && s.IsActive);

                        if (hasWorkingDoctor && slotDateTime > DateTime.Now)
                        {
                            timeSlots.Add(new TimeSlotDto
                            {
                                Time = slotTime,
                                DisplayTime = $"{hour:D2}:{minute:D2}",
                                IsAvailable = true
                            });
                        }
                    }
                }

                return Ok(timeSlots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available time slots");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("available-doctors")]
        public async Task<ActionResult<IEnumerable<AvailableDoctorDto>>> GetAvailableDoctors(
            [FromQuery] DateTime appointmentDate,
            [FromQuery] string specialty)
        {
            try
            {
                _logger.LogInformation("Getting available doctors for {Date} with specialty {Specialty}", 
                    appointmentDate, specialty);

                var availableDoctors = await _doctorShiftRepository.GetAvailableDoctorsAsync(
                    appointmentDate, specialty);

                var result = availableDoctors.Select(d => new AvailableDoctorDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Specialty = d.Specialty,
                    Email = d.Email
                }).ToList();

                return Ok(result);
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
                var shifts = await _doctorShiftRepository.GetByDoctorIdAsync(doctorId);
                var doctor = await _doctorRepository.GetByIdAsync(doctorId);

                if (doctor == null)
                {
                    return NotFound($"Doctor with ID {doctorId} not found");
                }

                var result = shifts.Select(s => new DoctorShiftDto
                {
                    Id = s.Id,
                    DoctorId = s.DoctorId,
                    DoctorName = doctor.Name,
                    DayOfWeek = s.DayOfWeek.ToString(),
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    IsActive = s.IsActive
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting doctor schedule");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class TimeSlotDto
    {
        public TimeSpan Time { get; set; }
        public string DisplayTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class AvailableDoctorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class DoctorShiftDto
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}