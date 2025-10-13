using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IMedicalRecordRepository
    {
        Task<MedicalRecord?> GetByIdAsync(int id);
        Task<MedicalRecord?> GetByAppointmentIdAsync(int appointmentId);
        Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId, int page, int pageSize);
        Task<IEnumerable<MedicalRecord>> GetByDoctorIdAsync(int doctorId);
        Task<MedicalRecord> CreateAsync(MedicalRecord medicalRecord);
        Task<MedicalRecord?> UpdateAsync(MedicalRecord medicalRecord);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<MedicalRecord>> GetAllAsync();
    }
}
