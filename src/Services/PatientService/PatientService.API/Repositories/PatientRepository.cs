using Microsoft.EntityFrameworkCore;
using PatientService.API.Data;
using PatientService.API.Models;

namespace PatientService.API.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly PatientDbContext _context;
        private readonly ILogger<PatientRepository> _logger;

        public PatientRepository(PatientDbContext context, ILogger<PatientRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Patient> CreateAsync(Patient patient)
        {
            try
            {
                _logger.LogInformation("Creating patient: {Name}, Age: {Age}, Email: {Email}", 
                    patient.Name, patient.Age, patient.Email);

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created patient with ID: {PatientId}", patient.Id);
                return patient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                throw;
            }
        }

        public async Task<Patient?> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting patient with ID: {PatientId}", id);
                
                var patient = await _context.Patients.FindAsync(id);
                
                if (patient == null)
                {
                    _logger.LogWarning("Patient with ID {PatientId} not found", id);
                }
                else
                {
                    _logger.LogInformation("Found patient: {Name}", patient.Name);
                }

                return patient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient with ID: {PatientId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Patient>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all patients");
                
                var patients = await _context.Patients.ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} patients", patients.Count);
                return patients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all patients");
                throw;
            }
        }

        public async Task<Patient?> UpdateAsync(Patient patient)
        {
            try
            {
                _logger.LogInformation("Updating patient with ID: {PatientId}", patient.Id);

                var existingPatient = await _context.Patients.FindAsync(patient.Id);
                if (existingPatient == null)
                {
                    _logger.LogWarning("Patient with ID {PatientId} not found for update", patient.Id);
                    return null;
                }

                existingPatient.Name = patient.Name;
                existingPatient.Age = patient.Age;
                existingPatient.Email = patient.Email;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated patient with ID: {PatientId}", patient.Id);
                return existingPatient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient with ID: {PatientId}", patient.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting patient with ID: {PatientId}", id);

                var patient = await _context.Patients.FindAsync(id);
                if (patient == null)
                {
                    _logger.LogWarning("Patient with ID {PatientId} not found for deletion", id);
                    return false;
                }

                _context.Patients.Remove(patient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted patient with ID: {PatientId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient with ID: {PatientId}", id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                return await _context.Patients.AnyAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if patient exists with ID: {PatientId}", id);
                throw;
            }
        }
    }
}
