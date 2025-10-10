using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IMedicalRecordHistoryRepository
    {
        Task<MedicalRecordHistory?> GetByIdAsync(int id);
        Task<List<MedicalRecordHistory>> GetByMedicalRecordIdAsync(int medicalRecordId);
        Task AddAsync(MedicalRecordHistory history);
        Task SaveChangesAsync();
    }
}
