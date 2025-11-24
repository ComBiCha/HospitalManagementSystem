using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IPaymentRepository
    {
        Task<IEnumerable<Payment>> GetAllAsync();
        Task<Payment?> GetByIdAsync(int id);
        Task<Payment?> GetByAppointmentIdAsync(int appointmentId);
        Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Payment>> GetByMedicalRecordIdAsync(int medicalRecordId);
        Task<IEnumerable<Payment>> GetCompletedPaymentsByMedicalRecordIdAsync(int medicalRecordId);
        Task<Payment?> GetPendingPaymentByMedicalRecordIdAsync(int medicalRecordId);
        Task<Payment?> GetPendingDepositByAppointmentIdAsync(int appointmentId);
        Task<Payment?> GetPendingOrCompletedDepositByAppointmentIdAsync(int appointmentId);
        Task<IEnumerable<Payment>> GetCompletedDepositsByAppointmentIdAsync(int appointmentId);
        Task<Payment?> GetLatestPendingBookingFeePaymentForAppointmentAsync(int appointmentId);
        Task<IEnumerable<Payment>> GetByStatusAsync(string status);
        Task<Payment> CreateAsync(Payment payment);
        Task<Payment?> UpdateAsync(Payment payment);
        Task<bool> DeleteAsync(int id);
    }
}