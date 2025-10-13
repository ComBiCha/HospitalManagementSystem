using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Application.DTOs.Appointment;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/patient-portal")]
    [Authorize(Roles = "Patient")]
    public class PatientPortalController : ControllerBase
    {
        private readonly MedicalRecordApplicationService _medicalRecordService;
        private readonly AppointmentApplicationService _appointmentService;

        public PatientPortalController(MedicalRecordApplicationService medicalRecordService, AppointmentApplicationService appointmentService)
        {
            _medicalRecordService = medicalRecordService;
            _appointmentService = appointmentService;
        }

        [HttpGet("medical-records")]
        public async Task<IActionResult> GetMyMedicalRecords([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var patientId = GetCurrentUserPatientId();
            if (!patientId.HasValue)
            {
                return Forbid();
            }

            var records = await _medicalRecordService.GetMedicalRecordsForPatientAsync(patientId.Value, page, pageSize);
            return Ok(records);
        }

        [HttpGet("appointments")]
        public async Task<IActionResult> GetMyAppointments([FromQuery] AppointmentFilterDto filter, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var patientId = GetCurrentUserPatientId();
            if (!patientId.HasValue)
            {
                return Forbid();
            }

            var appointments = await _appointmentService.GetAppointmentsForPatientAsync(patientId.Value, filter, page, pageSize);
            return Ok(appointments);
        }

        private int? GetCurrentUserPatientId()
        {
            var patientIdClaim = User.FindFirst("PatientId")?.Value;
            return int.TryParse(patientIdClaim, out int patientId) ? patientId : null;
        }
    }
}
