using Microsoft.EntityFrameworkCore;
using AppointmentService.API.Data;
using AppointmentService.API.Models;

namespace AppointmentService.API.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly AppointmentDbContext _context;
        private readonly ILogger<AppointmentRepository> _logger;

        public AppointmentRepository(AppointmentDbContext context, ILogger<AppointmentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Appointment> CreateAsync(Appointment appointment)
        {
            try
            {
                _logger.LogInformation("Creating appointment: PatientId={PatientId}, DoctorId={DoctorId}, Date={Date}", 
                    appointment.PatientId, appointment.DoctorId, appointment.Date);

                // Ensure DateTime is in UTC for PostgreSQL
                appointment.Date = appointment.Date.Kind == DateTimeKind.Utc 
                    ? appointment.Date 
                    : DateTime.SpecifyKind(appointment.Date, DateTimeKind.Utc);

                appointment.CreatedAt = DateTime.UtcNow;
                appointment.UpdatedAt = DateTime.UtcNow;

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created appointment with ID: {AppointmentId}", appointment.Id);
                return appointment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment");
                throw;
            }
        }

        public async Task<Appointment?> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting appointment with ID: {AppointmentId}", id);
                
                var appointment = await _context.Appointments.FindAsync(id);
                
                if (appointment == null)
                {
                    _logger.LogWarning("Appointment with ID {AppointmentId} not found", id);
                }

                return appointment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointment with ID: {AppointmentId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Appointment>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all appointments");
                
                var appointments = await _context.Appointments
                    .OrderBy(a => a.Date)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} appointments", appointments.Count);
                return appointments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all appointments");
                throw;
            }
        }

        public async Task<Appointment?> UpdateAsync(Appointment appointment)
        {
            try
            {
                _logger.LogInformation("Updating appointment with ID: {AppointmentId}", appointment.Id);

                var existingAppointment = await _context.Appointments.FindAsync(appointment.Id);
                if (existingAppointment == null)
                {
                    _logger.LogWarning("Appointment with ID {AppointmentId} not found for update", appointment.Id);
                    return null;
                }

                existingAppointment.PatientId = appointment.PatientId;
                existingAppointment.DoctorId = appointment.DoctorId;
                
                // Ensure DateTime is in UTC for PostgreSQL
                existingAppointment.Date = appointment.Date.Kind == DateTimeKind.Utc 
                    ? appointment.Date 
                    : DateTime.SpecifyKind(appointment.Date, DateTimeKind.Utc);
                    
                existingAppointment.Status = appointment.Status;
                existingAppointment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated appointment with ID: {AppointmentId}", appointment.Id);
                return existingAppointment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment with ID: {AppointmentId}", appointment.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting appointment with ID: {AppointmentId}", id);

                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                {
                    _logger.LogWarning("Appointment with ID {AppointmentId} not found for deletion", id);
                    return false;
                }

                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted appointment with ID: {AppointmentId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting appointment with ID: {AppointmentId}", id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                return await _context.Appointments.AnyAsync(a => a.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if appointment exists with ID: {AppointmentId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId)
        {
            try
            {
                _logger.LogInformation("Getting appointments for patient: {PatientId}", patientId);
                
                var appointments = await _context.Appointments
                    .Where(a => a.PatientId == patientId)
                    .OrderBy(a => a.Date)
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} appointments for patient {PatientId}", appointments.Count, patientId);
                return appointments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for patient: {PatientId}", patientId);
                throw;
            }
        }

        public async Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId)
        {
            try
            {
                _logger.LogInformation("Getting appointments for doctor: {DoctorId}", doctorId);
                
                var appointments = await _context.Appointments
                    .Where(a => a.DoctorId == doctorId)
                    .OrderBy(a => a.Date)
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} appointments for doctor {DoctorId}", appointments.Count, doctorId);
                return appointments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for doctor: {DoctorId}", doctorId);
                throw;
            }
        }

        public async Task<IEnumerable<Appointment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Getting appointments between {StartDate} and {EndDate}", startDate, endDate);
                
                var appointments = await _context.Appointments
                    .Where(a => a.Date >= startDate && a.Date <= endDate)
                    .OrderBy(a => a.Date)
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} appointments in date range", appointments.Count);
                return appointments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for date range");
                throw;
            }
        }

        public async Task<bool> HasConflictingAppointmentAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId = null)
        {
            try
            {
                // Ensure DateTime is in UTC for PostgreSQL
                var utcAppointmentDate = appointmentDate.Kind == DateTimeKind.Utc 
                    ? appointmentDate 
                    : DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

                // Check for appointments within 1 hour window
                var startTime = utcAppointmentDate.AddMinutes(-30);
                var endTime = utcAppointmentDate.AddMinutes(30);

                var query = _context.Appointments
                    .Where(a => a.DoctorId == doctorId && 
                               a.Date >= startTime && 
                               a.Date <= endTime &&
                               a.Status != "Cancelled");

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
    }
}
