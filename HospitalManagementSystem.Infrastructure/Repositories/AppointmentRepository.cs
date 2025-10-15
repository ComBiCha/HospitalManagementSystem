using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Specifications;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly HospitalDbContext _context;
        private readonly ILogger<AppointmentRepository> _logger;

        public AppointmentRepository(HospitalDbContext context, ILogger<AppointmentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Appointment?> GetByIdAsync(int id)
        {
            return await _context.Appointments.FindAsync(id);
        }

                public async Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId)

                {

                    return await _context.Appointments

                        .Where(a => a.PatientId == patientId)

                        .ToListAsync();

                }

        

                public async Task<IEnumerable<Appointment>> GetByDoctorIdAndDateAsync(int doctorId, DateTime date)

                {

                    var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);

                    return await _context.Appointments

                        .Where(a => a.DoctorId == doctorId && a.Date.Date == utcDate.Date)

                        .ToListAsync();

                }

        

                public async Task<Appointment> GetByIdWithIncludesAsync(int id)

                {

                    return await _context.Appointments

                        .Include(a => a.Patient)

                        .Include(a => a.Doctor)

                        .FirstOrDefaultAsync(a => a.Id == id);

                }

        

        public async Task<IEnumerable<Appointment>> GetAppointmentsWithPaymentsForCleanupAsync(DateTime cleanupTime)
        {
            return await _context.Appointments
                .Where(a => a.Status == "PendingPayment" && a.PaymentExpiresAt != null && a.PaymentExpiresAt < cleanupTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.Appointments.Where(a => a.DoctorId == doctorId).ToListAsync();
        }

        public async Task<Appointment> CreateAsync(Appointment appointment)
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task<Appointment?> UpdateAsync(Appointment appointment)
        {
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var appointment = await GetByIdAsync(id);
            if (appointment == null) return false;

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Appointment>> GetAllAsync()
        {
            return await _context.Appointments.ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetAsync(ISpecification<Appointment> spec)
        {
            return await SpecificationEvaluator<Appointment>.GetQuery(_context.Appointments.AsQueryable(), spec).ToListAsync();
        }

        public IQueryable<Appointment> GetQueryable(ISpecification<Appointment> spec)
        {
            return SpecificationEvaluator<Appointment>.GetQuery(_context.Appointments.AsQueryable(), spec);
        }

        public async Task<bool> HasConflictingAppointmentAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId = null)
        {
            try
            {
                var utcAppointmentDate = appointmentDate.Kind == DateTimeKind.Utc
                    ? appointmentDate
                    : DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

                var startTime = utcAppointmentDate.AddMinutes(-30);
                var endTime = utcAppointmentDate.AddMinutes(30);

                var query = _context.Appointments
                    .Where(a => a.DoctorId == doctorId &&
                               a.Date >= startTime &&
                               a.Date <= endTime &&
                               a.Status != "Cancelled" &&
                               a.Status != "ExpiredPayment");

                if (excludeAppointmentId.HasValue)
                {
                    query = query.Where(a => a.Id != excludeAppointmentId.Value);
                }

                var hasConflict = await query.AnyAsync();

                _logger.LogInformation("Conflict check for doctor {DoctorId} at {Date}: {HasConflict}",
                    doctorId, utcAppointmentDate, hasConflict);

                return hasConflict;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for conflicting appointments");
                throw;
            }
        }

        public async Task<List<Appointment>> GetDoctorAppointmentsAsync(int doctorId, DateTime startDate, DateTime endDate)
        {
            return await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.DoctorId == doctorId &&
                           a.Date >= startDate &&
                           a.Date < endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();
        }
        public async Task<Appointment?> GetByIdWithDetailsAsync(int appointmentId)
        {
            return await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.MedicalRecord)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
        }
    }

    public static class SpecificationEvaluator<TEntity> where TEntity : class
    {
        public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> inputQuery, ISpecification<TEntity> specification)
        {
            var query = inputQuery;

            if (specification.Criteria != null)
            {
                query = query.Where(specification.Criteria);
            }

            query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

            query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

            return query;
        }
    }
}
