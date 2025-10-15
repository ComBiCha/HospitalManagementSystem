using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly HospitalDbContext _context;

        public PaymentRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Payment>> GetAllAsync()
        {
            return await _context.Payments
                .Include(p => p.Patient)
                .Include(p => p.Appointment)
                .Include(p => p.MedicalRecord)
                .ToListAsync();
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            return await _context.Payments
                .Include(p => p.Patient)
                .Include(p => p.Appointment)
                .Include(p => p.MedicalRecord)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Payment?> GetByAppointmentIdAsync(int appointmentId)
        {
            return await _context.Payments
                .Include(p => p.Patient)
                .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId);
        }

        public async Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId)
        {
            return await _context.Payments
                .Where(p => p.PatientId == patientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Payment>> GetByMedicalRecordIdAsync(int medicalRecordId)
        {
            return await _context.Payments
                .Where(p => p.MedicalRecordId == medicalRecordId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Payment?> GetPendingPaymentByMedicalRecordIdAsync(int medicalRecordId)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(p => p.MedicalRecordId == medicalRecordId && p.Status == PaymentStatuses.Pending);
        }

        public async Task<IEnumerable<Payment>> GetByStatusAsync(string status)
        {
            return await _context.Payments
                .Where(p => p.Status == status)
                .ToListAsync();
        }

        public async Task<Payment> CreateAsync(Payment payment)
        {
            payment.CreatedAt = DateTime.UtcNow;
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment?> UpdateAsync(Payment payment)
        {
            var existing = await _context.Payments.FindAsync(payment.Id);
            if (existing == null) return null;

            existing.Status = payment.Status;
            existing.TransactionId = payment.TransactionId;
            existing.StripeSessionId = payment.StripeSessionId;
            existing.StripePaymentIntentId = payment.StripePaymentIntentId;
            existing.FailureReason = payment.FailureReason;
            existing.PaidAt = payment.PaidAt;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null) return false;

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}