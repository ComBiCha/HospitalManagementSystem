using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs.MedicalRecord;
using System.Text.Json;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MedicalRecordsController : ControllerBase
    {
        private readonly IMedicalRecordRepository _medicalRecordRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPrescriptionItemRepository _prescriptionItemRepository;
        private readonly IMedicalRecordHistoryRepository _medicalRecordHistoryRepository;
        private readonly MedicalRecordApplicationService _medicalRecordApplicationService;
        private readonly AppointmentExaminationService _appointmentExaminationService;
        private readonly IDoctorAttendanceRepository _attendanceRepository;
        private readonly ILogger<MedicalRecordsController> _logger;
        private readonly IWebHostEnvironment _env;

        public MedicalRecordsController(
            IMedicalRecordRepository medicalRecordRepository,
            IAppointmentRepository appointmentRepository,
            IPrescriptionItemRepository prescriptionItemRepository,
            IMedicalRecordHistoryRepository medicalRecordHistoryRepository,
            MedicalRecordApplicationService medicalRecordApplicationService,
            AppointmentExaminationService appointmentExaminationService,
            IDoctorAttendanceRepository attendanceRepository,
            ILogger<MedicalRecordsController> logger,
            IWebHostEnvironment env)
        {
            _medicalRecordRepository = medicalRecordRepository;
            _appointmentRepository = appointmentRepository;
            _prescriptionItemRepository = prescriptionItemRepository;
            _medicalRecordHistoryRepository = medicalRecordHistoryRepository;
            _medicalRecordApplicationService = medicalRecordApplicationService;
            _appointmentExaminationService = appointmentExaminationService;
            _attendanceRepository = attendanceRepository;
            _logger = logger;
            _env = env;
        }

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

                var medicalRecordDto = await _medicalRecordApplicationService.GetMedicalRecordByAppointmentAsync(appointmentId, doctorId);

                if (medicalRecordDto == null)
                {
                    return NotFound(new { message = "Medical record not found or access denied" });
                }

                return Ok(medicalRecordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching medical record by appointment");
                return StatusCode(500, new { message = "Lỗi khi tải hồ sơ" });
            }
        }

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

                var result = await _appointmentExaminationService.CanStartExaminationAsync(appointmentId, doctorId);

                if (!result.CanStart && result.Message == "Appointment not found")
                    return NotFound(new { message = result.Message });
                if (!result.CanStart && result.Message == "You can only examine your own appointments")
                    return Forbid(result.Message);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking examination eligibility");
                return StatusCode(500, new { message = "Lỗi hệ thống" });
            }
        }

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

                var (message, medicalRecord) = await _appointmentExaminationService.StartExaminationAsync(request.AppointmentId, doctorId);

                if (medicalRecord == null)
                {
                    if (message == "Appointment not found")
                        return NotFound(new { message });
                    if (message == "You can only examine your own appointments")
                        return Forbid(message);
                    return Ok(new { message });
                }

                return Ok(new
                {
                    message,
                    medicalRecord
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting examination");
                return StatusCode(500, new { message = "Lỗi khi bắt đầu khám bệnh" });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMedicalRecord(int id)
        {
            try
            {
                var medicalRecordDto = await _medicalRecordApplicationService.GetMedicalRecordByIdAsync(id);

                if (medicalRecordDto == null)
                {
                    return NotFound(new { message = "Medical record not found" });
                }

                return Ok(medicalRecordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching medical record");
                return StatusCode(500, new { message = "Lỗi khi tải hồ sơ" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UpdateMedicalRecord(int id, [FromBody] UpdateMedicalRecordRequest request)
        {
            try
            {
                var (message, medicalRecord) = await _medicalRecordApplicationService.UpdateMedicalRecordAsync(id, request);

                if (medicalRecord == null)
                {
                    return NotFound(new { message });
                }

                return Ok(new
                {
                    message,
                    medicalRecord
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
                var (message, success) = await _medicalRecordApplicationService.CompleteMedicalRecordAsync(id);

                if (!success)
                {
                    return NotFound(new { message });
                }

                return Ok(new { message, success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing medical record");
                return StatusCode(500, new { message = "Lỗi khi hoàn thành" });
            }
        }

        [HttpPost("{id}/hospitalize")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> HospitalizeMedicalRecord(int id)
        {
            try
            {
                var (message, medicalRecord) = await _medicalRecordApplicationService.HospitalizeMedicalRecordAsync(id);

                if (medicalRecord == null)
                {
                    return NotFound(new { message });
                }

                return Ok(new { message, medicalRecord });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hospitalizing patient");
                return StatusCode(500, new { message = "Lỗi khi nhập viện" });
            }
        }

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

        [HttpGet("{id}/prescription-items")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> GetPrescriptionItems(int id)
        {
            try
            {
                var items = await _prescriptionItemRepository.GetByMedicalRecordIdAsync(id);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching prescription items");
                return StatusCode(500, new { message = "Lỗi khi tải danh sách" });
            }
        }

        [HttpPost("prescription-items/{id}/request-cancel")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> RequestCancelPrescriptionItem(int id, [FromBody] CancelItemRequest request)
        {
            try
            {
                var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
                {
                    return BadRequest(new { message = "Doctor ID not found in token" });
                }

                var (success, message) = await _medicalRecordApplicationService.RequestCancelPrescriptionItemAsync(id, doctorId, request.Reason);
                if (!success) return NotFound(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting cancel prescription item");
                return StatusCode(500, new { message = "Lỗi khi yêu cầu hủy" });
            }
        }

        [HttpPost("prescription-items/{id}/approve-cancel")]
        [Authorize(Roles = "Admin,Nurse")]
        public async Task<IActionResult> ApproveCancelPrescriptionItem(int id)
        {
            try
            {
                var (success, message) = await _medicalRecordApplicationService.ApproveCancelPrescriptionItemAsync(id);
                if (!success) return NotFound(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving cancel prescription item");
                return StatusCode(500, new { message = "Lỗi khi duyệt hủy" });
            }
        }

        [HttpGet("{id}/history")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> GetMedicalRecordHistory(int id)
        {
            try
            {
                var history = await _medicalRecordHistoryRepository.GetByMedicalRecordIdAsync(id);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching medical record history");
                return StatusCode(500, new { message = "Lỗi khi tải lịch sử" });
            }
        }

        [HttpPost("prescription-items/{id}/complete")]
        [Authorize(Roles = "Doctor,Admin,Nurse")]
        public async Task<IActionResult> CompletePrescriptionItem(int id)
        {
            try
            {
                var (success, message) = await _medicalRecordApplicationService.CompletePrescriptionItemAsync(id);

                if (!success)
                {
                    return BadRequest(new { message });
                }

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing prescription item {Id}", id);
                return StatusCode(500, new { message = "Lỗi khi hoàn thành xét nghiệm" });
            }
        }
    }

    // Request DTOs
    public class StartExaminationRequest
    {
        public int AppointmentId { get; set; }
    }

    public class CancelItemRequest
    {
        public string Reason { get; set; } = string.Empty;
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
}

