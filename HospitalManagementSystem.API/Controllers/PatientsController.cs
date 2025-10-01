using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Domain.Caching;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Application.Services;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientRepository _patientRepository;
        private readonly ICacheService _cacheService;
        private readonly PatientService _patientService;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(
            IPatientRepository patientRepository,
            ICacheService cacheService,
            PatientService patientService,
            ILogger<PatientsController> logger)
        {
            _patientRepository = patientRepository;
            _cacheService = cacheService;
            _patientService = patientService;
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
        [HttpPost("full")]
        public async Task<IActionResult> CreateFullPatient([FromBody] PatientCreateDto dto)
        {
            try
            {
                var createdPatient = await _patientService.CreatePatientAsync(dto);
                return CreatedAtAction(nameof(GetPatient), new { id = createdPatient.Id }, new
                {
                    createdPatient.Id,
                    createdPatient.Name,
                    createdPatient.Age,
                    createdPatient.Email,
                    createdPatient.Status,
                    createdPatient.CreatedAt,
                    createdPatient.UpdatedAt,
                    Identifiers = createdPatient.PatientIdentifiers.Select(x => new
                    {
                        x.Id,
                        x.EHRSystem,
                        x.ExternalId,
                        x.IdentifierType,
                        x.IsActive,
                        x.CreatedAt,
                        x.UpdatedAt
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient with identifiers");
                return StatusCode(500, new { message = "Error creating patient" });
            }
        }
        [HttpGet("{id}/identifiers")]
        public async Task<IActionResult> GetPatientIdentifiers(int id)
        {
            var identifiers = await _patientService.GetPatientIdentifiersAsync(id);
            if (identifiers == null || identifiers.Count == 0)
                return NotFound(new { message = "No identifiers found for this patient." });

            return Ok(identifiers.Select(x => new
            {
                x.Id,
                x.PatientId,
                x.EHRSystem,
                x.ExternalId,
                x.IdentifierType,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            }));
        }
        [HttpPost("{id}/identifiers")]
        public async Task<IActionResult> AddPatientIdentifier(int id, [FromBody] PatientIdentifiers identifier)
        {
            try
            {
                var patient = await _patientRepository.GetPatientByIdAsync(id);
                if (patient == null) return NotFound();

                identifier.PatientId = id;
                identifier.CreatedAt = DateTime.UtcNow;
                identifier.UpdatedAt = DateTime.UtcNow;
                
                var created = await _patientService.AddPatientIdentifierAsync(identifier);
                
                // Return DTO instead of entity to avoid circular reference
                return CreatedAtAction(nameof(GetPatientIdentifiers), new { id }, new
                {
                    created.Id,
                    created.PatientId,
                    created.EHRSystem,
                    created.ExternalId,
                    created.IdentifierType,
                    created.IsActive,
                    created.CreatedAt,
                    created.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding patient identifier for patient {PatientId}", id);
                return StatusCode(500, new { message = "Error adding patient identifier", error = ex.Message });
            }
        }
        [HttpPut("identifiers/{identifierId}")]
        public async Task<IActionResult> UpdatePatientIdentifier(int identifierId, [FromBody] PatientIdentifiers identifier)
        {
            try
            {
                if (identifierId != identifier.Id)
                {
                    return BadRequest("ID mismatch");
                }
                
                // Fetch existing identifier to preserve PatientId  
                var existing = await _patientService.GetPatientIdentifiersAsync(identifier.PatientId);
                var existingIdentifier = existing?.FirstOrDefault(x => x.Id == identifierId);
                
                if (existingIdentifier == null)
                {
                    return NotFound();
                }
                
                // Update only allowed fields, keep PatientId unchanged
                identifier.PatientId = existingIdentifier.PatientId;
                identifier.CreatedAt = existingIdentifier.CreatedAt;
                identifier.UpdatedAt = DateTime.UtcNow;
                
                var updated = await _patientService.UpdatePatientIdentifierAsync(identifier);
                
                if (updated == null)
                {
                    return NotFound();
                }
                
                // Return DTO instead of entity to avoid circular reference
                return Ok(new
                {
                    updated.Id,
                    updated.PatientId,
                    updated.EHRSystem,
                    updated.ExternalId,
                    updated.IdentifierType,
                    updated.IsActive,
                    updated.CreatedAt,
                    updated.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient identifier: {IdentifierId}", identifierId);
                return StatusCode(500, new { message = "Error updating patient identifier", error = ex.Message });
            }
        }
        
        [HttpDelete("identifiers/{identifierId}")]
        public async Task<IActionResult> DeletePatientIdentifier(int identifierId)
        {
            try
            {
                var result = await _patientService.DeletePatientIdentifierAsync(identifierId);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient identifier: {IdentifierId}", identifierId);
                return StatusCode(500, new { message = "Error deleting patient identifier", error = ex.Message });
            }
        }
        [HttpGet("{id}/ehr")]
        public async Task<IActionResult> GetPatientInfoFromEhr(int id, [FromQuery] EHRSystem ehrSystem = EHRSystem.Epic)
        {
            var info = await _patientService.GetPatientInfoFromEhrAsync(id, ehrSystem);
            if (info == null)
                return NotFound(new { message = $"No identifier or data found for EHR system '{ehrSystem}'." });

            return Ok(info);
        }

        [HttpGet("next")]
        public async Task<IActionResult> GetPatientsNext([FromQuery] int? lastId = null, [FromQuery] int pageSize = 20)
        {
            try
            {
                var baseUrl = $"{Request.Path}";
                var (data, nextLink, previousLink) = await _patientService.GetPatientsWithNextLinkAsync(lastId, pageSize, baseUrl);
                return Ok(new { data, nextLink, previousLink });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patients with next link");
                return StatusCode(500, new { message = "Error retrieving patients" });
            }
        }

        [HttpGet("ehr/search")]
        public async Task<IActionResult> SearchPatientsInEhr(
            [FromQuery] EHRSystem ehrSystem = EHRSystem.Epic,
            [FromQuery] string? name = null,
            [FromQuery] string? email = null,
            [FromQuery] string? phone = null,
            [FromQuery] string? gender = null,
            [FromQuery] string? identifier = null)
        {
            try
            {
                var results = await _patientService.SearchPatientsInEhrAsync(ehrSystem, name, email, phone, gender, identifier);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching patients in EHR system: {EHRSystem}", ehrSystem);
                return StatusCode(500, new { message = "Error searching patients in EHR system" });
            }
        }


    }
            public class PatientStatusRequest
        {
            public PatientStatus Status { get; set; }
            public string Action { get; set; } = "add"; // add, remove, set
        }
}