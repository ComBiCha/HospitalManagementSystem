using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly HospitalDbContext _context;
        private readonly ILogger<PatientRepository> _logger;

        public PatientRepository(HospitalDbContext context, ILogger<PatientRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Patient?> GetPatientByIdAsync(int id)
        {
            try
            {
                return await _context.Patients.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient by ID: {PatientId}", id);
                throw;
            }
        }

        public async Task<Patient?> GetPatientByEmailAsync(string email)
        {
            try
            {
                return await _context.Patients
                    .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient by email: {Email}", email);
                throw;
            }
        }

        public async Task<IEnumerable<Patient>> GetAllPatientsAsync(int page = 1, int pageSize = 50)
        {
            return await _context.Patients
                .OrderBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Patient>> GetPatientsByNameAsync(string name)
        {
            try
            {
                return await _context.Patients
                    .Where(p => p.Name.ToLower().Contains(name.ToLower()))
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patients by name: {Name}", name);
                throw;
            }
        }

        public async Task<Patient> CreatePatientAsync(Patient patient)
        {
            try
            {
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Patient created with ID: {PatientId}", patient.Id);
                return patient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                throw;
            }
        }

        public async Task<Patient?> UpdatePatientAsync(Patient patient)
        {
            try
            {
                var existingPatient = await _context.Patients.FindAsync(patient.Id);
                if (existingPatient == null)
                {
                    return null;
                }

                _context.Entry(existingPatient).CurrentValues.SetValues(patient);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Patient updated: {PatientId}", patient.Id);
                return existingPatient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient: {PatientId}", patient.Id);
                throw;
            }
        }

        public async Task<bool> DeletePatientAsync(int id)
        {
            try
            {
                var patient = await _context.Patients.FindAsync(id);
                if (patient == null)
                {
                    return false;
                }

                _context.Patients.Remove(patient);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Patient deleted: {PatientId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient: {PatientId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Patient>> GetRecentPatientsAsync(int count = 50)
        {
            try
            {
                return await _context.Patients
                    .OrderByDescending(p => p.Id) // In reality, use CreatedAt if available
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent patients");
                throw;
            }
        }

        public async Task<int> GetPatientCountAsync()
        {
            try
            {
                return await _context.Patients.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient count");
                throw;
            }
        }
    }
}