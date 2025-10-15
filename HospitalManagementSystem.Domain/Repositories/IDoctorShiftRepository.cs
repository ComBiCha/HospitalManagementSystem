using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IDoctorShiftRepository
    {
        Task<DoctorShift> CreateAsync(DoctorShift shift);
        Task<DoctorShift?> GetByIdAsync(int id);
        Task<IEnumerable<DoctorShift>> GetAllAsync();
        Task<IEnumerable<DoctorShift>> GetByDoctorIdAsync(int doctorId);
        Task<IEnumerable<DoctorShift>> GetByDayOfWeekAsync(DayOfWeek dayOfWeek);
        Task<DoctorShift?> UpdateAsync(DoctorShift shift);
        Task<bool> DeleteAsync(int id);
        Task<bool> IsDoctorWorkingAsync(int doctorId, DateTime date);
        Task<IEnumerable<Doctor>> GetAvailableDoctorsAsync(DateTime appointmentDate, string specialty);
        Task<List<DoctorShift>> GetDoctorShiftsAsync(int doctorId);
        Task<IEnumerable<DoctorShift>> GetShiftsByDoctorAndDayAsync(int doctorId, DayOfWeek dayOfWeek);
        Task<bool> HasAnyActiveShiftOnDateAsync(DateTime date);
        Task<IEnumerable<DayOfWeek>> GetActiveShiftDaysAsync(int doctorId);
    }
}