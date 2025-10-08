using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Infrastructure.Repositories;

public class DoctorAttendanceRepository : IDoctorAttendanceRepository
{
    private readonly HospitalDbContext _context;

    public DoctorAttendanceRepository(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<DoctorAttendance?> GetByIdAsync(int id)
    {
        return await _context.DoctorAttendances
            .Include(a => a.Doctor)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<DoctorAttendance?> GetTodayAttendanceAsync(int doctorId, DateTime date)
    {
        var dateOnly = date.Date;
        return await _context.DoctorAttendances
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.ShiftDate.Date == dateOnly);
    }

    public async Task<List<DoctorAttendance>> GetDoctorAttendancesAsync(int doctorId, DateTime startDate, DateTime endDate)
    {
        return await _context.DoctorAttendances
            .Include(a => a.Shift)
            .Where(a => a.DoctorId == doctorId && 
                       a.ShiftDate >= startDate.Date && 
                       a.ShiftDate <= endDate.Date)
            .OrderByDescending(a => a.ShiftDate)
            .ToListAsync();
    }

    public async Task<DoctorAttendance> CreateAsync(DoctorAttendance attendance)
    {
        if (attendance.CreatedAt == default)
        {
            attendance.CreatedAt = DateTime.UtcNow;
        }
        _context.DoctorAttendances.Add(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    public async Task<DoctorAttendance> UpdateAsync(DoctorAttendance attendance)
    {
        if (attendance.UpdatedAt == default || attendance.UpdatedAt == null)
        {
            attendance.UpdatedAt = DateTime.UtcNow;
        }
        _context.DoctorAttendances.Update(attendance);
        await _context.SaveChangesAsync();
        return attendance;
    }

    public async Task<bool> CanCheckInAsync(int doctorId, int shiftId, DateTime checkInTime)
    {
        var shift = await _context.DoctorShifts.FindAsync(shiftId);
        if (shift == null || !shift.IsActive) return false;

        // Check if shift is for today
        var today = checkInTime.Date;
        var dayOfWeek = (int)checkInTime.DayOfWeek;
        
        if ((int)shift.DayOfWeek != dayOfWeek) return false;

        // Check if check-in time is within 10 minutes before shift start
        var shiftStartDateTime = today.Add(shift.StartTime);
        var earliestCheckIn = shiftStartDateTime.AddMinutes(-10);
        
        return checkInTime >= earliestCheckIn && checkInTime <= shiftStartDateTime.AddHours(1);
    }

    public async Task<DoctorAttendance?> GetActiveAttendanceAsync(int doctorId)
    {
        return await _context.DoctorAttendances
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && 
                                     a.Status == "CheckedIn" && 
                                     a.CheckOutTime == null);
    }
}
