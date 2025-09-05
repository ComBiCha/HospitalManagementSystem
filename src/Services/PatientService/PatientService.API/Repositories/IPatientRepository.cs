using PatientService.API.Models;

namespace PatientService.API.Repositories
{
    public interface IPatientRepository
    {
        Task<Patient?> GetPatientByIdAsync(int id);
        Task<Patient?> GetPatientByEmailAsync(string email);
        Task<IEnumerable<Patient>> GetAllPatientsAsync();
        Task<IEnumerable<Patient>> GetPatientsByNameAsync(string name);
        Task<Patient> CreatePatientAsync(Patient patient);
        Task<Patient?> UpdatePatientAsync(Patient patient);
        Task<bool> DeletePatientAsync(int id);
        
        // Additional methods for cached repository
        Task<IEnumerable<Patient>> GetRecentPatientsAsync(int count = 50);
        Task<int> GetPatientCountAsync();
    }
}