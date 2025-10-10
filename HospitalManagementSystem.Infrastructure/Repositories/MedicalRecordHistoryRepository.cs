using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class MedicalRecordHistoryRepository : IMedicalRecordHistoryRepository
    {
        private readonly HospitalDbContext _context;

        public MedicalRecordHistoryRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<MedicalRecordHistory?> GetByIdAsync(int id)
        {
            return await _context.MedicalRecordHistories.FindAsync(id);
        }

        public async Task<List<MedicalRecordHistory>> GetByMedicalRecordIdAsync(int medicalRecordId)
        {
            return await _context.MedicalRecordHistories
                .Where(h => h.MedicalRecordId == medicalRecordId)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(MedicalRecordHistory history)
        {
            await _context.MedicalRecordHistories.AddAsync(history);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
