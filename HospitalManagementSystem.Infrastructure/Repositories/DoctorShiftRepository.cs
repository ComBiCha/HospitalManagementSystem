using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class DoctorShiftRepository : IDoctorShiftRepository
    {
        private readonly HospitalDbContext _context;
        private readonly ILogger<DoctorShiftRepository> _logger;

        public DoctorShiftRepository(HospitalDbContext context, ILogger<DoctorShiftRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DoctorShift> CreateAsync(DoctorShift shift)
        {
            try
            {
                shift.CreatedAt = DateTime.UtcNow;
                _context.DoctorShifts.Add(shift);
                await _context.SaveChangesAsync();
                return shift;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating doctor shift");
                throw;
            }
        }

        public async Task<DoctorShift?> GetByIdAsync(int id)
        {
            return await _context.DoctorShifts
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<DoctorShift>> GetAllAsync()
        {
            return await _context.DoctorShifts
                .Include(s => s.Doctor)
                .Where(s => s.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<DoctorShift>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.DoctorShifts
                .Where(s => s.DoctorId == doctorId && s.IsActive)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();
        }

        public async Task<IEnumerable<DoctorShift>> GetByDayOfWeekAsync(DayOfWeek dayOfWeek)
        {
            return await _context.DoctorShifts
                .Include(s => s.Doctor)
                .Where(s => s.DayOfWeek == dayOfWeek && s.IsActive)
                .ToListAsync();
        }

        public async Task<DoctorShift?> UpdateAsync(DoctorShift shift)
        {
            try
            {
                var existing = await _context.DoctorShifts.FindAsync(shift.Id);
                if (existing == null) return null;

                existing.DayOfWeek = shift.DayOfWeek;
                existing.StartTime = shift.StartTime;
                existing.EndTime = shift.EndTime;
                existing.IsActive = shift.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating doctor shift");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var shift = await _context.DoctorShifts.FindAsync(id);
                if (shift == null) return false;

                shift.IsActive = false;
                shift.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting doctor shift");
                throw;
            }
        }

        public async Task<bool> IsDoctorWorkingAsync(int doctorId, DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;
            var time = date.TimeOfDay;

            return await _context.DoctorShifts
                .AnyAsync(s => s.DoctorId == doctorId 
                    && s.DayOfWeek == dayOfWeek 
                    && s.StartTime <= time 
                    && s.EndTime >= time 
                    && s.IsActive);
        }

        public async Task<IEnumerable<Doctor>> GetAvailableDoctorsAsync(DateTime appointmentDate, string specialty)
        {
            var dateParam = new NpgsqlParameter("p_appointment_time", appointmentDate.ToUniversalTime());
            var specialtyParam = new NpgsqlParameter("p_specialty", specialty);

            var doctors = await _context.Doctors
                .FromSqlRaw("SELECT * FROM get_available_doctors(@p_appointment_time, @p_specialty)", 
                            dateParam, specialtyParam)
                .ToListAsync();

            return doctors;
        }


        public async Task<List<DoctorShift>> GetDoctorShiftsAsync(int doctorId)
        {
            return await _context.DoctorShifts
                .Where(s => s.DoctorId == doctorId)
                .ToListAsync();
        }

        public async Task<IEnumerable<DoctorShift>> GetShiftsByDoctorAndDayAsync(int doctorId, DayOfWeek dayOfWeek)
        {
            return await _context.DoctorShifts
                .Where(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && s.IsActive)
                .ToListAsync();
        }

        public async Task<bool> HasAnyActiveShiftOnDateAsync(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;
            return await _context.DoctorShifts.AnyAsync(s => s.DayOfWeek == dayOfWeek && s.IsActive);
        }

        public async Task<IEnumerable<DayOfWeek>> GetActiveShiftDaysAsync(int doctorId)
        {
            return await _context.DoctorShifts
                .Where(s => s.DoctorId == doctorId && s.IsActive)
                .Select(s => s.DayOfWeek)
                .Distinct()
                .ToListAsync();
        }
    }
}