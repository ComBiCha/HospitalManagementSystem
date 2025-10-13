using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Specifications;
using System.Linq;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IAppointmentRepository
    {
        Task<Appointment?> GetByIdAsync(int id);
        Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId);
        Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId);
        Task<Appointment> CreateAsync(Appointment appointment);
        Task<Appointment?> UpdateAsync(Appointment appointment);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<Appointment>> GetAllAsync();
        Task<IEnumerable<Appointment>> GetAsync(ISpecification<Appointment> spec);
        IQueryable<Appointment> GetQueryable(ISpecification<Appointment> spec);
        Task<bool> HasConflictingAppointmentAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId = null);
        Task<List<Appointment>> GetDoctorAppointmentsAsync(int doctorId, DateTime startDate, DateTime endDate);
        Task<Appointment?> GetByIdWithDetailsAsync(int appointmentId);
    }
}
