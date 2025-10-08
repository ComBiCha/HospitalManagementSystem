using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MedicalRecordsController : ControllerBase
    {
        private readonly HospitalDbContext _context;
        private readonly IMedicalRecordRepository _medicalRecordRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IDoctorAttendanceRepository _attendanceRepository;
        private readonly ILogger<MedicalRecordsController> _logger;
        private readonly IWebHostEnvironment _env;

        public MedicalRecordsController(
            HospitalDbContext context,
            IMedicalRecordRepository medicalRecordRepository,
            IAppointmentRepository appointmentRepository,
            IDoctorAttendanceRepository attendanceRepository,
            ILogger<MedicalRecordsController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _medicalRecordRepository = medicalRecordRepository;
            _appointmentRepository = appointmentRepository;
            _attendanceRepository = attendanceRepository;
            _logger = logger;
            _env = env;
        }

        // GET: api/MedicalRecords/by-appointment/{appointmentId}
        [HttpGet("by-appointment/{appointmentId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMedicalRecordByAppointment(int appointmentId)
        {
            try
            {
                var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
                {
                    return BadRequest(new { message = "Doctor ID not found in token" });
                }

                var medicalRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(appointmentId);

                if (medicalRecord == null)
                {
                    return NotFound(new { message = "Medical record not found for this appointment" });
                }

                // Verify doctor owns this appointment
                if (medicalRecord.DoctorId != doctorId)
                {
                    return Forbid("You can only view your own medical records");
                }

                // Calculate paid amount
                var paidAmount = medicalRecord.Payments
                    .Where(p => p.Status == "Completed" && 
                               (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
                    .Sum(p => p.Amount);

                // Create DTO
                var response = new MedicalRecordDto
                {
                    Id = medicalRecord.Id,
                    AppointmentId = medicalRecord.AppointmentId,
                    PatientId = medicalRecord.PatientId,
                    DoctorId = medicalRecord.DoctorId,
                    Diagnosis = medicalRecord.Diagnosis,
                    Symptoms = medicalRecord.Symptoms,
                    Treatment = medicalRecord.Treatment,
                    Prescription = medicalRecord.Prescription,
                    Notes = medicalRecord.Notes,
                    ConsultationFee = medicalRecord.ConsultationFee,
                    MedicineFee = medicalRecord.MedicineFee,
                    TestFee = medicalRecord.TestFee,
                    OtherFee = medicalRecord.OtherFee,
                    PaidAmount = paidAmount,
                    PaymentStatus = medicalRecord.PaymentStatus,
                    CreatedAt = medicalRecord.CreatedAt,
                    UpdatedAt = medicalRecord.UpdatedAt,
                    Patient = medicalRecord.Patient != null ? new PatientDto
                    {
                        Id = medicalRecord.Patient.Id,
                        Name = medicalRecord.Patient.Name,
                        Age = medicalRecord.Patient.Age,
                        Email = medicalRecord.Patient.Email,
                        Status = medicalRecord.Patient.Status.ToString()
                    } : null,
                    Doctor = medicalRecord.Doctor != null ? new DoctorDto
                    {
                        Id = medicalRecord.Doctor.Id,
                        Name = medicalRecord.Doctor.Name,
                        Specialty = medicalRecord.Doctor.Specialty,
                        Email = medicalRecord.Doctor.Email
                    } : null,
                    Appointment = medicalRecord.Appointment != null ? new SimpleAppointmentDto
                    {
                        Id = medicalRecord.Appointment.Id,
                        Date = medicalRecord.Appointment.Date,
                        Status = medicalRecord.Appointment.Status
                    } : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching medical record by appointment");
                return StatusCode(500, new { message = "Lỗi khi tải hồ sơ" });
            }
        }

        // GET: api/MedicalRecords/can-start-examination/{appointmentId}
        [HttpGet("can-start-examination/{appointmentId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CanStartExamination(int appointmentId)
        {
            try
            {
                var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
                {
                    return BadRequest(new { message = "Doctor ID not found in token" });
                }

                var appointment = await _context.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                    .Include(a => a.MedicalRecord)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { message = "Appointment not found" });
                }

                if (appointment.DoctorId != doctorId)
                {
                    return Forbid("You can only examine your own appointments");
                }

                // Check appointment status first - allow viewing completed records without check-in
                if (appointment.Status == "Completed")
                {
                    return Ok(new { 
                        canStart = true,  // Cho phép xem lại
                        message = "Đã hoàn thành khám bệnh - Xem hồ sơ",
                        medicalRecordId = appointment.MedicalRecord?.Id,
                        isReadOnly = true  // Frontend sẽ hiển thị read-only mode
                    });
                }

                // Check if doctor is checked in (only for active appointments)
                var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
                var today = nowVn.Date;

                var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
                var activeAttendance = await _context.DoctorAttendances
                    .FirstOrDefaultAsync(a => a.DoctorId == doctorId && 
                                             a.ShiftDate == todayUtc && 
                                             a.CheckInTime != null && 
                                             a.CheckOutTime == null);

                if (activeAttendance == null)
                {
                    return Ok(new { 
                        canStart = false, 
                        message = "Bác sĩ chưa check-in!" 
                    });
                }

                // Check if appointment time is within 10 minutes
                var appointmentTime = appointment.Date;
                var tenMinutesBefore = appointmentTime.AddMinutes(-10);
                var nowUtc = DateTime.UtcNow;

                if (nowUtc < tenMinutesBefore)
                {
                    return Ok(new { 
                        canStart = false, 
                        message = $"Chỉ có thể bắt đầu khám từ {TimeZoneInfo.ConvertTimeFromUtc(tenMinutesBefore, vnTimeZone):HH:mm}" 
                    });
                }

                // Check appointment status for active cases
                if (appointment.Status == "Hospitalized")
                {
                    return Ok(new { 
                        canStart = true, 
                        message = "Bệnh nhân đang nhập viện - có thể tiếp tục điều trị",
                        medicalRecordId = appointment.MedicalRecord?.Id,
                        isReadOnly = false
                    });
                }

                if (appointment.Status != "Scheduled" && appointment.Status != "InProgress")
                {
                    return Ok(new { 
                        canStart = false, 
                        message = $"Appointment status: {appointment.Status}" 
                    });
                }

                return Ok(new { 
                    canStart = true, 
                    message = "Có thể bắt đầu khám",
                    medicalRecordId = appointment.MedicalRecord?.Id,
                    isReadOnly = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking examination eligibility");
                return StatusCode(500, new { message = "Lỗi hệ thống" });
            }
        }

        // POST: api/MedicalRecords/start-examination
        [HttpPost("start-examination")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> StartExamination([FromBody] StartExaminationRequest request)
        {
            try
            {
                var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
                {
                    return BadRequest(new { message = "Doctor ID not found in token" });
                }

                var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
                if (appointment == null)
                {
                    return NotFound(new { message = "Appointment not found" });
                }

                if (appointment.DoctorId != doctorId)
                {
                    return Forbid("You can only examine your own appointments");
                }

                // Check if medical record already exists
                var existingRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(request.AppointmentId);
                if (existingRecord != null)
                {
                    return Ok(new { 
                        message = "Medical record already exists",
                        medicalRecord = existingRecord
                    });
                }

                // Create new draft medical record
                var medicalRecord = new MedicalRecord
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = doctorId,
                    Diagnosis = "[]",
                    Symptoms = "[]",
                    Treatment = "",
                    Prescription = "[]",
                    Notes = "",
                    ConsultationFee = 200000,
                    MedicineFee = 0,
                    TestFee = 0,
                    OtherFee = 0,
                    PaidAmount = 0,
                    PaymentStatus = "Unpaid",
                    CreatedAt = DateTime.UtcNow
                };

                var createdRecord = await _medicalRecordRepository.CreateAsync(medicalRecord);

                // Update appointment status to InProgress
                appointment.Status = "InProgress";
                appointment.UpdatedAt = DateTime.UtcNow;
                await _appointmentRepository.UpdateAsync(appointment);

                // Return DTO to avoid circular reference
                var response = new MedicalRecordDto
                {
                    Id = createdRecord.Id,
                    AppointmentId = createdRecord.AppointmentId,
                    PatientId = createdRecord.PatientId,
                    DoctorId = createdRecord.DoctorId,
                    Diagnosis = createdRecord.Diagnosis,
                    Symptoms = createdRecord.Symptoms,
                    Treatment = createdRecord.Treatment,
                    Prescription = createdRecord.Prescription,
                    Notes = createdRecord.Notes,
                    ConsultationFee = createdRecord.ConsultationFee,
                    MedicineFee = createdRecord.MedicineFee,
                    TestFee = createdRecord.TestFee,
                    OtherFee = createdRecord.OtherFee,
                    PaidAmount = createdRecord.PaidAmount,
                    PaymentStatus = createdRecord.PaymentStatus,
                    CreatedAt = createdRecord.CreatedAt,
                    UpdatedAt = createdRecord.UpdatedAt
                };

                return Ok(new { 
                    message = "Bắt đầu khám bệnh thành công",
                    medicalRecord = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting examination");
                return StatusCode(500, new { message = "Lỗi khi bắt đầu khám bệnh" });
            }
        }

        // GET: api/MedicalRecords/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMedicalRecord(int id)
        {
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);

                if (medicalRecord == null)
                {
                    return NotFound(new { message = "Medical record not found" });
                }

                // Calculate paid amount from payments
                var paidAmount = medicalRecord.Payments
                    .Where(p => p.Status == "Completed" && 
                               (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
                    .Sum(p => p.Amount);

                // Create DTO to avoid circular reference
                var response = new MedicalRecordDto
                {
                    Id = medicalRecord.Id,
                    AppointmentId = medicalRecord.AppointmentId,
                    PatientId = medicalRecord.PatientId,
                    DoctorId = medicalRecord.DoctorId,
                    Diagnosis = medicalRecord.Diagnosis,
                    Symptoms = medicalRecord.Symptoms,
                    Treatment = medicalRecord.Treatment,
                    Prescription = medicalRecord.Prescription,
                    Notes = medicalRecord.Notes,
                    ConsultationFee = medicalRecord.ConsultationFee,
                    MedicineFee = medicalRecord.MedicineFee,
                    TestFee = medicalRecord.TestFee,
                    OtherFee = medicalRecord.OtherFee,
                    PaidAmount = paidAmount,
                    PaymentStatus = medicalRecord.PaymentStatus,
                    CreatedAt = medicalRecord.CreatedAt,
                    UpdatedAt = medicalRecord.UpdatedAt,
                    Patient = medicalRecord.Patient != null ? new PatientDto
                    {
                        Id = medicalRecord.Patient.Id,
                        Name = medicalRecord.Patient.Name,
                        Age = medicalRecord.Patient.Age,
                        Email = medicalRecord.Patient.Email,
                        Status = medicalRecord.Patient.Status.ToString()
                    } : null,
                    Doctor = medicalRecord.Doctor != null ? new DoctorDto
                    {
                        Id = medicalRecord.Doctor.Id,
                        Name = medicalRecord.Doctor.Name,
                        Specialty = medicalRecord.Doctor.Specialty,
                        Email = medicalRecord.Doctor.Email
                    } : null,
                    Appointment = medicalRecord.Appointment != null ? new SimpleAppointmentDto
                    {
                        Id = medicalRecord.Appointment.Id,
                        Date = medicalRecord.Appointment.Date,
                        Status = medicalRecord.Appointment.Status
                    } : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching medical record");
                return StatusCode(500, new { message = "Lỗi khi tải hồ sơ" });
            }
        }

        // PUT: api/MedicalRecords/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UpdateMedicalRecord(int id, [FromBody] UpdateMedicalRecordRequest request)
        {
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);

                if (medicalRecord == null)
                {
                    return NotFound(new { message = "Medical record not found" });
                }

                // Update fields
                if (request.Diagnosis != null) medicalRecord.Diagnosis = request.Diagnosis;
                if (request.Symptoms != null) medicalRecord.Symptoms = request.Symptoms;
                if (request.Treatment != null) medicalRecord.Treatment = request.Treatment;
                if (request.Prescription != null) medicalRecord.Prescription = request.Prescription;
                if (request.Notes != null) medicalRecord.Notes = request.Notes;
                if (request.MedicineFee.HasValue) medicalRecord.MedicineFee = request.MedicineFee.Value;
                if (request.TestFee.HasValue) medicalRecord.TestFee = request.TestFee.Value;
                if (request.OtherFee.HasValue) medicalRecord.OtherFee = request.OtherFee.Value;

                // Calculate paid amount
                var paidAmount = medicalRecord.Payments
                    .Where(p => p.Status == "Completed" && 
                               (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
                    .Sum(p => p.Amount);

                medicalRecord.PaidAmount = paidAmount;

                await _medicalRecordRepository.UpdateAsync(medicalRecord);

                // Return DTO to avoid circular reference
                var response = new MedicalRecordDto
                {
                    Id = medicalRecord.Id,
                    AppointmentId = medicalRecord.AppointmentId,
                    PatientId = medicalRecord.PatientId,
                    DoctorId = medicalRecord.DoctorId,
                    Diagnosis = medicalRecord.Diagnosis,
                    Symptoms = medicalRecord.Symptoms,
                    Treatment = medicalRecord.Treatment,
                    Prescription = medicalRecord.Prescription,
                    Notes = medicalRecord.Notes,
                    ConsultationFee = medicalRecord.ConsultationFee,
                    MedicineFee = medicalRecord.MedicineFee,
                    TestFee = medicalRecord.TestFee,
                    OtherFee = medicalRecord.OtherFee,
                    PaidAmount = paidAmount,
                    PaymentStatus = medicalRecord.PaymentStatus,
                    CreatedAt = medicalRecord.CreatedAt,
                    UpdatedAt = medicalRecord.UpdatedAt
                };

                return Ok(new { 
                    message = "Cập nhật thành công",
                    medicalRecord = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating medical record");
                return StatusCode(500, new { message = "Lỗi khi cập nhật" });
            }
        }

        // POST: api/MedicalRecords/{id}/complete
        [HttpPost("{id}/complete")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CompleteMedicalRecord(int id)
        {
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);

                if (medicalRecord == null)
                {
                    return NotFound(new { message = "Medical record not found" });
                }

                // Update payment status based on paid amount
                var totalFee = medicalRecord.ConsultationFee + medicalRecord.MedicineFee + 
                              medicalRecord.TestFee + medicalRecord.OtherFee;
                
                var paidAmount = medicalRecord.Payments
                    .Where(p => p.Status == "Completed")
                    .Sum(p => p.Amount);

                medicalRecord.PaidAmount = paidAmount;

                if (paidAmount >= totalFee)
                {
                    medicalRecord.PaymentStatus = "FullyPaid";
                }
                else if (paidAmount > 0)
                {
                    medicalRecord.PaymentStatus = "PartiallyPaid";
                }
                else
                {
                    medicalRecord.PaymentStatus = "Unpaid";
                }

                // Update appointment status
                if (medicalRecord.Appointment != null)
                {
                    medicalRecord.Appointment.Status = "Completed";
                    medicalRecord.Appointment.UpdatedAt = DateTime.UtcNow;
                    await _appointmentRepository.UpdateAsync(medicalRecord.Appointment);
                }

                await _medicalRecordRepository.UpdateAsync(medicalRecord);

                // Return simple response without circular reference
                return Ok(new { 
                    message = "Hoàn thành khám bệnh",
                    success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing medical record");
                return StatusCode(500, new { message = "Lỗi khi hoàn thành" });
            }
        }

        // POST: api/MedicalRecords/{id}/hospitalize
        [HttpPost("{id}/hospitalize")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> HospitalizeMedicalRecord(int id)
        {
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);

                if (medicalRecord == null)
                {
                    return NotFound(new { message = "Medical record not found" });
                }

                // Update appointment status to Hospitalized
                if (medicalRecord.Appointment != null)
                {
                    medicalRecord.Appointment.Status = "Hospitalized";
                    medicalRecord.Appointment.UpdatedAt = DateTime.UtcNow;
                    await _appointmentRepository.UpdateAsync(medicalRecord.Appointment);
                }

                await _medicalRecordRepository.UpdateAsync(medicalRecord);

                return Ok(new { 
                    message = "Đã chuyển nhập viện",
                    medicalRecord = medicalRecord
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hospitalizing patient");
                return StatusCode(500, new { message = "Lỗi khi nhập viện" });
            }
        }

        // GET: api/MedicalRecords/reference-data/diagnoses
        [HttpGet("reference-data/diagnoses")]
        public async Task<IActionResult> GetDiagnoses()
        {
            try
            {
                // Try multiple possible paths
                var possiblePaths = new[]
                {
                    Path.Combine(_env.ContentRootPath, "..", "src", "diagnoses.json"),
                    Path.Combine(_env.ContentRootPath, "src", "diagnoses.json"),
                    "/app/src/diagnoses.json",
                    "/src/diagnoses.json"
                };

                string? filePath = null;
                foreach (var path in possiblePaths)
                {
                    var normalizedPath = Path.GetFullPath(path);
                    _logger.LogInformation("Trying path: {Path}", normalizedPath);
                    if (System.IO.File.Exists(normalizedPath))
                    {
                        filePath = normalizedPath;
                        break;
                    }
                }

                if (filePath == null)
                {
                    _logger.LogError("Diagnoses file not found. ContentRootPath: {ContentRoot}", _env.ContentRootPath);
                    return NotFound(new { 
                        message = "File diagnoses.json không tìm thấy",
                        contentRoot = _env.ContentRootPath,
                        triedPaths = possiblePaths
                    });
                }
                
                _logger.LogInformation("Loading diagnoses from: {FilePath}", filePath);
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                
                // Try to parse as object first, then extract array
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                try
                {
                    // Try direct array deserialization
                    var diagnoses = JsonSerializer.Deserialize<List<DiagnosisItem>>(json, options);
                    if (diagnoses != null)
                    {
                        return Ok(diagnoses);
                    }
                }
                catch
                {
                    // Try as object with diagnoses property
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    // Check if it's an object with a property containing the array
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Array)
                            {
                                var diagnoses = JsonSerializer.Deserialize<List<DiagnosisItem>>(property.Value.GetRawText(), options);
                                return Ok(diagnoses);
                            }
                        }
                    }
                }
                
                return Ok(new List<DiagnosisItem>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading diagnoses");
                return StatusCode(500, new { message = "Lỗi khi tải chẩn đoán", error = ex.Message });
            }
        }

        // GET: api/MedicalRecords/reference-data/symptoms
        [HttpGet("reference-data/symptoms")]
        public async Task<IActionResult> GetSymptoms()
        {
            try
            {
                var possiblePaths = new[]
                {
                    Path.Combine(_env.ContentRootPath, "..", "src", "symptoms.json"),
                    Path.Combine(_env.ContentRootPath, "src", "symptoms.json"),
                    "/app/src/symptoms.json",
                    "/src/symptoms.json"
                };

                string? filePath = null;
                foreach (var path in possiblePaths)
                {
                    var normalizedPath = Path.GetFullPath(path);
                    if (System.IO.File.Exists(normalizedPath))
                    {
                        filePath = normalizedPath;
                        break;
                    }
                }

                if (filePath == null)
                {
                    _logger.LogError("Symptoms file not found");
                    return NotFound(new { message = "File symptoms.json không tìm thấy" });
                }
                
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                try
                {
                    var symptoms = JsonSerializer.Deserialize<List<SymptomItem>>(json, options);
                    if (symptoms != null) return Ok(symptoms);
                }
                catch
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Array)
                            {
                                var symptoms = JsonSerializer.Deserialize<List<SymptomItem>>(property.Value.GetRawText(), options);
                                return Ok(symptoms);
                            }
                        }
                    }
                }
                
                return Ok(new List<SymptomItem>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading symptoms");
                return StatusCode(500, new { message = "Lỗi khi tải triệu chứng", error = ex.Message });
            }
        }

        // GET: api/MedicalRecords/reference-data/drugs
        [HttpGet("reference-data/drugs")]
        public async Task<IActionResult> GetDrugs()
        {
            try
            {
                var possiblePaths = new[]
                {
                    Path.Combine(_env.ContentRootPath, "..", "src", "drugs.json"),
                    Path.Combine(_env.ContentRootPath, "src", "drugs.json"),
                    "/app/src/drugs.json",
                    "/src/drugs.json"
                };

                string? filePath = null;
                foreach (var path in possiblePaths)
                {
                    var normalizedPath = Path.GetFullPath(path);
                    if (System.IO.File.Exists(normalizedPath))
                    {
                        filePath = normalizedPath;
                        break;
                    }
                }

                if (filePath == null)
                {
                    _logger.LogError("Drugs file not found");
                    return NotFound(new { message = "File drugs.json không tìm thấy" });
                }
                
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                try
                {
                    var drugs = JsonSerializer.Deserialize<List<DrugItem>>(json, options);
                    if (drugs != null) return Ok(drugs);
                }
                catch
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Array)
                            {
                                var drugs = JsonSerializer.Deserialize<List<DrugItem>>(property.Value.GetRawText(), options);
                                return Ok(drugs);
                            }
                        }
                    }
                }
                
                return Ok(new List<DrugItem>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading drugs");
                return StatusCode(500, new { message = "Lỗi khi tải thuốc", error = ex.Message });
            }
        }

        // GET: api/MedicalRecords/reference-data/medical-tests
        [HttpGet("reference-data/medical-tests")]
        public async Task<IActionResult> GetMedicalTests()
        {
            try
            {
                var possiblePaths = new[]
                {
                    Path.Combine(_env.ContentRootPath, "..", "src", "medical_tests.json"),
                    Path.Combine(_env.ContentRootPath, "src", "medical_tests.json"),
                    "/app/src/medical_tests.json",
                    "/src/medical_tests.json"
                };

                string? filePath = null;
                foreach (var path in possiblePaths)
                {
                    var normalizedPath = Path.GetFullPath(path);
                    if (System.IO.File.Exists(normalizedPath))
                    {
                        filePath = normalizedPath;
                        break;
                    }
                }

                if (filePath == null)
                {
                    _logger.LogError("Medical tests file not found");
                    return NotFound(new { message = "File medical_tests.json không tìm thấy" });
                }
                
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                // Parse as array of categories
                var categories = JsonSerializer.Deserialize<List<MedicalTestCategory>>(json, options);
                
                // Flatten to get all tests
                var allTests = new List<MedicalTestItem>();
                if (categories != null)
                {
                    foreach (var category in categories)
                    {
                        allTests.AddRange(category.tests);
                    }
                }
                
                return Ok(allTests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading medical tests");
                return StatusCode(500, new { message = "Lỗi khi tải xét nghiệm", error = ex.Message });
            }
        }
    }

    // Request DTOs
    public class StartExaminationRequest
    {
        public int AppointmentId { get; set; }
    }

    public class UpdateMedicalRecordRequest
    {
        public string? Diagnosis { get; set; }
        public string? Symptoms { get; set; }
        public string? Treatment { get; set; }
        public string? Prescription { get; set; }
        public string? Notes { get; set; }
        public decimal? MedicineFee { get; set; }
        public decimal? TestFee { get; set; }
        public decimal? OtherFee { get; set; }
    }

    // Reference data models
    public class DiagnosisItem
    {
        public string code { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
    }

    public class SymptomItem
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
    }

    public class DrugItem
    {
        public string name { get; set; } = string.Empty;
        public string dosage { get; set; } = string.Empty;
        public decimal fee { get; set; }
        public string unit { get; set; } = string.Empty;
    }

    public class MedicalTestItem
    {
        public string testName { get; set; } = string.Empty;
        public string subcategory { get; set; } = string.Empty;
        public decimal fee { get; set; }
    }

    public class MedicalTestCategory
    {
        public string category { get; set; } = string.Empty;
        public List<MedicalTestItem> tests { get; set; } = new();
    }

    // Response DTOs to avoid circular reference
    public class MedicalRecordDto
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;
        public string Prescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal ConsultationFee { get; set; }
        public decimal MedicineFee { get; set; }
        public decimal TestFee { get; set; }
        public decimal OtherFee { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public PatientDto? Patient { get; set; }
        public DoctorDto? Doctor { get; set; }
        public SimpleAppointmentDto? Appointment { get; set; }
    }

    public class PatientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class DoctorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SimpleAppointmentDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

