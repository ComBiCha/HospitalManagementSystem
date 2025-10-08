
using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories;

public interface IDoctorAttendanceRepository
{
    Task<DoctorAttendance?> GetByIdAsync(int id);
    Task<DoctorAttendance?> GetTodayAttendanceAsync(int doctorId, DateTime date);
    Task<List<DoctorAttendance>> GetDoctorAttendancesAsync(int doctorId, DateTime startDate, DateTime endDate);
    Task<DoctorAttendance> CreateAsync(DoctorAttendance attendance);
    Task<DoctorAttendance> UpdateAsync(DoctorAttendance attendance);
    Task<bool> CanCheckInAsync(int doctorId, int shiftId, DateTime checkInTime);
    Task<DoctorAttendance?> GetActiveAttendanceAsync(int doctorId);
}
