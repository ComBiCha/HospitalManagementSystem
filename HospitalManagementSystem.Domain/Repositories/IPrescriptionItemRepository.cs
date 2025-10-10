using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IPrescriptionItemRepository
    {
        Task<PrescriptionItem?> GetByIdAsync(int id);
        Task<List<PrescriptionItem>> GetByMedicalRecordIdAsync(int medicalRecordId);
        Task<PrescriptionItem?> GetByMedicalRecordAndCodeAsync(int medicalRecordId, string itemCode);
        Task AddAsync(PrescriptionItem item);
        Task UpdateAsync(PrescriptionItem item);
        Task DeleteAsync(PrescriptionItem item);
        Task SaveChangesAsync();
    }
}
