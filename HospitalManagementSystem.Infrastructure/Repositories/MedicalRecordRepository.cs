using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HospitalManagementSystem.Domain.Specifications;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class MedicalRecordRepository : IMedicalRecordRepository
    {
        private readonly HospitalDbContext _context;
        private readonly ILogger<MedicalRecordRepository> _logger;

        public MedicalRecordRepository(HospitalDbContext context, ILogger<MedicalRecordRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MedicalRecord?> GetByIdAsync(int id)
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Payments)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<MedicalRecord?> GetByAppointmentIdAsync(int appointmentId)
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Payments)
                .FirstOrDefaultAsync(m => m.AppointmentId == appointmentId);
        }

        public async Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId)
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Payments)
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId, int page, int pageSize)
        {
            return await _context.MedicalRecords
                .AsNoTracking()
                .Include(m => m.Patient) // Include Patient
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.Appointment.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<MedicalRecord>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Payments)
                .Where(m => m.DoctorId == doctorId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<MedicalRecord> CreateAsync(MedicalRecord medicalRecord)
        {
            medicalRecord.CreatedAt = DateTime.UtcNow;
            _context.MedicalRecords.Add(medicalRecord);
            await _context.SaveChangesAsync();
            return medicalRecord;
        }

        public async Task<MedicalRecord?> UpdateAsync(MedicalRecord medicalRecord)
        {
            var existing = await _context.MedicalRecords.FindAsync(medicalRecord.Id);
            if (existing == null) return null;

            existing.Diagnosis = medicalRecord.Diagnosis;
            existing.Symptoms = medicalRecord.Symptoms;
            existing.Treatment = medicalRecord.Treatment;
            existing.Prescription = medicalRecord.Prescription;
            existing.Notes = medicalRecord.Notes;
            existing.ConsultationFee = medicalRecord.ConsultationFee;
            existing.MedicineFee = medicalRecord.MedicineFee;
            existing.TestFee = medicalRecord.TestFee;
            existing.OtherFee = medicalRecord.OtherFee;
            existing.PaidAmount = medicalRecord.PaidAmount;
            existing.PaymentStatus = medicalRecord.PaymentStatus;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var medicalRecord = await _context.MedicalRecords.FindAsync(id);
            if (medicalRecord == null) return false;

            _context.MedicalRecords.Remove(medicalRecord);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<MedicalRecord>> GetAllAsync()
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Payments)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MedicalRecord>> GetUnpaidMedicalRecordsAsync(int page, int pageSize)
        {
            return await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Where(m => (m.PaymentStatus == "Unpaid" || m.PaymentStatus == "PartiallyPaid") && m.Appointment.Status == "Completed")
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<MedicalRecord>> GetRefundableMedicalRecordsAsync(int page, int pageSize)
        {
            var refundableRecords = await _context.MedicalRecords
                .Include(m => m.Patient)
                    .ThenInclude(p => p.PatientIdentifiers) // Include PatientIdentifiers
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Where(m => m.Appointment.Status == "Completed")
                .ToListAsync();

            return refundableRecords
                .Where(m => m.PaidAmount > m.TotalFee)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public IQueryable<MedicalRecord> GetQueryable(ISpecification<MedicalRecord> spec)
        {
            return SpecificationEvaluator<MedicalRecord>.GetQuery(_context.MedicalRecords.AsQueryable(), spec);
        }
    }
}
