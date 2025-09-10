using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IBillingRepository
    {
        Task<IEnumerable<Billing>> GetAllAsync();
        Task<Billing?> GetByIdAsync(int id);
        Task<Billing?> GetByAppointmentIdAsync(int appointmentId);
        Task<IEnumerable<Billing>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Billing>> GetByStatusAsync(string status);
        Task<Billing> CreateAsync(Billing billing);
        Task<Billing?> UpdateAsync(Billing billing);
        Task<bool> DeleteAsync(int id);
    }
}
