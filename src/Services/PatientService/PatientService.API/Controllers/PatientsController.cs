using Microsoft.AspNetCore.Mvc;
using PatientService.API.Models;
using PatientService.API.Repositories;

namespace PatientService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientRepository _patientRepository;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(IPatientRepository patientRepository, ILogger<PatientsController> logger)
        {
            _patientRepository = patientRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get all patients
        /// </summary>
        /// <returns>List of patients</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Patient>>> GetPatients()
        {
            try
            {
                _logger.LogInformation("Getting all patients via REST API");
                var patients = await _patientRepository.GetAllAsync();
                return Ok(patients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all patients");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get patient by ID
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>Patient details</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Patient>> GetPatient(int id)
        {
            try
            {
                _logger.LogInformation("Getting patient with ID: {PatientId} via REST API", id);
                
                var patient = await _patientRepository.GetByIdAsync(id);
                if (patient == null)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                return Ok(patient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient with ID: {PatientId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new patient
        /// </summary>
        /// <param name="patient">Patient data</param>
        /// <returns>Created patient</returns>
        [HttpPost]
        public async Task<ActionResult<Patient>> CreatePatient([FromBody] CreatePatientRequest request)
        {
            try
            {
                _logger.LogInformation("Creating patient via REST API: Name={Name}, Age={Age}, Email={Email}", 
                    request.Name, request.Age, request.Email);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var patient = new Patient
                {
                    Name = request.Name,
                    Age = request.Age,
                    Email = request.Email
                };

                var createdPatient = await _patientRepository.CreateAsync(patient);
                return CreatedAtAction(nameof(GetPatient), new { id = createdPatient.Id }, createdPatient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update an existing patient
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <param name="request">Updated patient data</param>
        /// <returns>Updated patient</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<Patient>> UpdatePatient(int id, [FromBody] UpdatePatientRequest request)
        {
            try
            {
                _logger.LogInformation("Updating patient with ID: {PatientId} via REST API", id);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var patient = new Patient
                {
                    Id = id,
                    Name = request.Name,
                    Age = request.Age,
                    Email = request.Email
                };

                var updatedPatient = await _patientRepository.UpdateAsync(patient);
                if (updatedPatient == null)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                return Ok(updatedPatient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient with ID: {PatientId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete a patient
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePatient(int id)
        {
            try
            {
                _logger.LogInformation("Deleting patient with ID: {PatientId} via REST API", id);

                var deleted = await _patientRepository.DeleteAsync(id);
                if (!deleted)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient with ID: {PatientId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Check if patient exists
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>Boolean result</returns>
        [HttpHead("{id}")]
        public async Task<IActionResult> PatientExists(int id)
        {
            try
            {
                var exists = await _patientRepository.ExistsAsync(id);
                return exists ? Ok() : NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if patient exists with ID: {PatientId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // DTOs for REST API
    public class CreatePatientRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class UpdatePatientRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
