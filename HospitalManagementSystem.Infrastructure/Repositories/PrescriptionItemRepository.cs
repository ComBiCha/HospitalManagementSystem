using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class PrescriptionItemRepository : IPrescriptionItemRepository
    {
        private readonly HospitalDbContext _context;

        public PrescriptionItemRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<PrescriptionItem?> GetByIdAsync(int id)
        {
            return await _context.PrescriptionItems.FindAsync(id);
        }

        public async Task<List<PrescriptionItem>> GetByMedicalRecordIdAsync(int medicalRecordId)
        {
            return await _context.PrescriptionItems
                .Where(p => p.MedicalRecordId == medicalRecordId)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<PrescriptionItem?> GetActiveByMedicalRecordAndCodeAsync(int medicalRecordId, string itemCode)
        {
            return await _context.PrescriptionItems
                .FirstOrDefaultAsync(p => p.MedicalRecordId == medicalRecordId && 
                                        p.ItemCode.ToLower() == itemCode.ToLower() &&
                                        p.Status != "Cancelled" &&
                                        p.Status != "Completed");
        }

        public async Task AddAsync(PrescriptionItem item)
        {
            await _context.PrescriptionItems.AddAsync(item);
        }

        public Task UpdateAsync(PrescriptionItem item)
        {
            _context.PrescriptionItems.Update(item);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(PrescriptionItem item)
        {
            _context.PrescriptionItems.Remove(item);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
