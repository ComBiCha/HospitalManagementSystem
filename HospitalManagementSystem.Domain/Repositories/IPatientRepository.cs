using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IPatientRepository
    {
        Task<Patient?> GetPatientByIdAsync(int id);
        Task<List<PatientIdentifiers>> GetPatientIdentifiersAsync(int patientId);
        Task<Patient?> GetPatientByEmailAsync(string email);
        Task<IEnumerable<Patient>> GetAllPatientsAsync(int page = 1, int pageSize = 50);
        Task<IEnumerable<Patient>> GetPatientsByNameAsync(string name);
        Task<Patient> CreatePatientAsync(Patient patient);
        Task<Patient?> UpdatePatientAsync(Patient patient);
        Task<bool> DeletePatientAsync(int id);

        // Additional methods for cached repository
        Task<IEnumerable<Patient>> GetRecentPatientsAsync(int count = 50);
        Task<int> GetPatientCountAsync();
        Task<(List<Patient> Data, string? NextLink, string? PreviousLink)> GetPatientsWithNextLinkAsync(int? lastId = null, int pageSize = 20, string baseUrl = "");
        Task<PatientIdentifiers> AddPatientIdentifierAsync(PatientIdentifiers identifier);
        Task<PatientIdentifiers?> UpdatePatientIdentifierAsync(PatientIdentifiers identifier);
        Task<bool> DeletePatientIdentifierAsync(int identifierId);
    }
}