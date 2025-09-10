using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class BillingRepository : IBillingRepository
    {
        private readonly HospitalDbContext _context;

        public BillingRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Billing>> GetAllAsync()
        {
            return await _context.Billings.ToListAsync();
        }

        public async Task<Billing?> GetByIdAsync(int id)
        {
            return await _context.Billings.FindAsync(id);
        }

        public async Task<Billing?> GetByAppointmentIdAsync(int appointmentId)
        {
            return await _context.Billings.FirstOrDefaultAsync(b => b.AppointmentId == appointmentId);
        }

        public async Task<IEnumerable<Billing>> GetByPatientIdAsync(int patientId)
        {
            return await _context.Billings.Where(b => b.PatientId == patientId).ToListAsync();
        }

        public async Task<IEnumerable<Billing>> GetByStatusAsync(string status)
        {
            return await _context.Billings.Where(b => b.Status == status).ToListAsync();
        }

        public async Task<Billing> CreateAsync(Billing billing)
        {
            _context.Billings.Add(billing);
            await _context.SaveChangesAsync();
            return billing;
        }

        public async Task<Billing?> UpdateAsync(Billing billing)
        {
            var existingBilling = await _context.Billings.FindAsync(billing.Id);
            if (existingBilling == null)
            {
                return null;
            }

            existingBilling.Amount = billing.Amount;
            existingBilling.PaymentMethod = billing.PaymentMethod;
            existingBilling.Status = billing.Status;
            existingBilling.Description = billing.Description;
            existingBilling.TransactionId = billing.TransactionId;
            existingBilling.FailureReason = billing.FailureReason;
            existingBilling.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return existingBilling;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var billing = await _context.Billings.FindAsync(id);
            if (billing == null)
            {
                return false;
            }

            _context.Billings.Remove(billing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
