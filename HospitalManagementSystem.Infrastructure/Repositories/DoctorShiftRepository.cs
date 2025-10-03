using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

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

        public async Task<IEnumerable<Doctor>> GetAvailableDoctorsAsync(DateTime appointmentDate, string specialty) //stored procedure
        {
            var requestedDate = appointmentDate.Kind == DateTimeKind.Utc 
                ? appointmentDate 
                : DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

            var dayOfWeek = requestedDate.DayOfWeek;
            var time = requestedDate.TimeOfDay;

            _logger.LogInformation("Searching doctors: DayOfWeek={DayOfWeek}, Time={Time}, Specialty={Specialty}, Requested={Requested}", 
                dayOfWeek, time, specialty, requestedDate);

            // Get doctors with matching specialty and working on that day/time
            var workingDoctors = await _context.DoctorShifts
                .Include(s => s.Doctor)
                .Where(s => s.DayOfWeek == dayOfWeek 
                    && s.StartTime <= time 
                    && s.EndTime >= time 
                    && s.IsActive
                    && s.Doctor.Specialty == specialty
                    && (s.Doctor.Status & DoctorStatus.Active) == DoctorStatus.Active)
                .Select(s => s.Doctor)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Found {Count} working doctors", workingDoctors.Count);

            // Filter out doctors who already have appointments at that time
            var availableDoctors = new List<Doctor>();
            foreach (var doctor in workingDoctors)
            {
                var doctorAppointments = await _context.Appointments
                    .Where(a => a.DoctorId == doctor.Id && a.Status != "Cancelled" && a.Status != "ExpiredPayment")
                    .ToListAsync();

                var hasConflict = doctorAppointments.Any(a => 
                {
                    var existingTimeLocal = a.Date.AddHours(7).TimeOfDay;
                    var requestedTime = requestedDate.TimeOfDay;
                    
                    var timeDiffMinutes = Math.Abs((existingTimeLocal - requestedTime).TotalMinutes);
                    
                    _logger.LogInformation(
                        "Doctor {DoctorId}: Existing={Existing} (DB: {DbTime}), Requested={Requested}, Diff={Diff} mins", 
                        doctor.Id, existingTimeLocal, a.Date, requestedTime, timeDiffMinutes);

                    return timeDiffMinutes < 30;
                });

                if (!hasConflict)
                {
                    availableDoctors.Add(doctor);
                    _logger.LogInformation("Doctor {DoctorId} ({DoctorName}) is available", doctor.Id, doctor.Name);
                }
                else
                {
                    _logger.LogInformation("Doctor {DoctorId} ({DoctorName}) has conflict", doctor.Id, doctor.Name);
                }
            }

            return availableDoctors;
        }
    }
}