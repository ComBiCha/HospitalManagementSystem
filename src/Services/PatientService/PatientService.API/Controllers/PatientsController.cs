using Microsoft.AspNetCore.Mvc;
using PatientService.API.Models;
using PatientService.API.Repositories;
using PatientService.API.Services.Caching;

namespace PatientService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientRepository _patientRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(
            IPatientRepository patientRepository,
            ICacheService cacheService,
            ILogger<PatientsController> logger)
        {
            _patientRepository = patientRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPatients([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var patients = await _patientRepository.GetAllPatientsAsync(page, pageSize);
                return Ok(patients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all patients");
                return StatusCode(500, new { message = "Error retrieving patients" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatient(int id)
        {
            try
            {
                var patient = await _patientRepository.GetPatientByIdAsync(id);
                if (patient == null)
                {
                    return NotFound();
                }
                return Ok(patient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient: {PatientId}", id);
                return StatusCode(500, new { message = "Error retrieving patient" });
            }
        }

        [HttpGet("email/{email}")]
        public async Task<IActionResult> GetPatientByEmail(string email)
        {
            try
            {
                var patient = await _patientRepository.GetPatientByEmailAsync(email);
                if (patient == null)
                {
                    return NotFound();
                }
                return Ok(patient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient by email: {Email}", email);
                return StatusCode(500, new { message = "Error retrieving patient" });
            }
        }

        [HttpGet("search/name/{name}")]
        public async Task<IActionResult> GetPatientsByName(string name)
        {
            try
            {
                var patients = await _patientRepository.GetPatientsByNameAsync(name);
                return Ok(patients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching patients by name: {Name}", name);
                return StatusCode(500, new { message = "Error searching patients" });
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentPatients([FromQuery] int count = 50)
        {
            try
            {
                var patients = await _patientRepository.GetRecentPatientsAsync(count);
                return Ok(patients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent patients");
                return StatusCode(500, new { message = "Error retrieving recent patients" });
            }
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetPatientCount()
        {
            try
            {
                var count = await _patientRepository.GetPatientCountAsync();
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient count");
                return StatusCode(500, new { message = "Error retrieving patient count" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePatient([FromBody] Patient patient)
        {
            try
            {
                var createdPatient = await _patientRepository.CreatePatientAsync(patient);
                return CreatedAtAction(nameof(GetPatient), new { id = createdPatient.Id }, createdPatient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                return StatusCode(500, new { message = "Error creating patient" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePatient(int id, [FromBody] Patient patient)
        {
            try
            {
                if (id != patient.Id)
                {
                    return BadRequest("ID mismatch");
                }

                var updatedPatient = await _patientRepository.UpdatePatientAsync(patient);
                if (updatedPatient == null)
                {
                    return NotFound();
                }

                return Ok(updatedPatient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient: {PatientId}", id);
                return StatusCode(500, new { message = "Error updating patient" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePatient(int id)
        {
            try
            {
                var result = await _patientRepository.DeletePatientAsync(id);
                if (!result)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient: {PatientId}", id);
                return StatusCode(500, new { message = "Error deleting patient" });
            }
        }

        // Cache management endpoints
        [HttpPost("cache/warmup")]
        public async Task<IActionResult> WarmupCache()
        {
            try
            {
                if (_patientRepository is CachedPatientRepository cachedRepo)
                {
                    await cachedRepo.WarmupCacheAsync();
                    return Ok(new { message = "Cache warmup completed successfully" });
                }

                return BadRequest(new { message = "Caching is not enabled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                return StatusCode(500, new { message = "Error during cache warmup" });
            }
        }

        [HttpPost("cache/clear")]
        public async Task<IActionResult> ClearCache([FromQuery] string? pattern = null)
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    if (_patientRepository is CachedPatientRepository cachedRepo)
                    {
                        await cachedRepo.ClearAllCacheAsync();
                    }
                }
                else
                {
                    await _cacheService.RemovePatternAsync(pattern);
                }

                return Ok(new { message = "Cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { message = "Error clearing cache" });
            }
        }

        [HttpGet("cache/info/{id}")]
        public async Task<IActionResult> GetCacheInfo(int id)
        {
            try
            {
                var cacheKey = CacheKeyGenerator.PatientById(id);
                var exists = await _cacheService.ExistsAsync(cacheKey);
                var ttl = await _cacheService.GetTtlAsync(cacheKey);

                return Ok(new
                {
                    cacheKey,
                    exists,
                    ttl = ttl?.TotalMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache info");
                return StatusCode(500, new { message = "Error getting cache info" });
            }
        }

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdatePatientStatus(int id, [FromBody] PatientStatusRequest request)
        {
            var patient = await _patientRepository.GetPatientByIdAsync(id);
            if (patient == null) return NotFound();

            // Thêm status mới
            if (request.Action == "add")
                patient.AddStatus(request.Status);

            // Xóa status
            else if (request.Action == "remove")
                patient.RemoveStatus(request.Status);

            // Set status hoàn toàn mới
            else if (request.Action == "set")
                patient.Status = request.Status;

            patient.UpdatedAt = DateTime.UtcNow;
            await _patientRepository.UpdatePatientAsync(patient);

            return Ok(new
            {
                Id = patient.Id,
                Name = patient.Name,
                Status = patient.Status.ToString(),
                IsActive = patient.IsActive,
                IsInTreatment = patient.IsInTreatment,
                IsEmergency = patient.IsEmergency
            });
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetPatientStatus(int id)
        {
            var patient = await _patientRepository.GetPatientByIdAsync(id);
            if (patient == null) return NotFound();

            return Ok(new
            {
                Id = patient.Id,
                Name = patient.Name,
                Status = patient.Status.ToString(),
                StatusValue = (int)patient.Status,
                Flags = new
                {
                    IsActive = patient.IsActive,
                    IsInTreatment = patient.IsInTreatment,
                    IsEmergency = patient.IsEmergency,
                    IsAdmitted = patient.IsAdmitted,
                    IsDischarged = patient.IsDischarged,
                    IsOnHold = patient.IsOnHold
                }
            });
        }


    }
            public class PatientStatusRequest
        {
            public PatientStatus Status { get; set; }
            public string Action { get; set; } = "add"; // add, remove, set
        }
}