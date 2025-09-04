using DoctorService.API.Models;

namespace DoctorService.API.Repositories
{
    public interface IDoctorRepository
    {
        Task<IEnumerable<Doctor>> GetAllAsync();
        Task<Doctor?> GetByIdAsync(int id);
        Task<Doctor?> GetByEmailAsync(string email);  // ✅ New method for email lookup
        Task<Doctor> CreateAsync(Doctor doctor);
        Task<Doctor?> UpdateAsync(Doctor doctor);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<bool> EmailExistsAsync(string email);    // ✅ Check email uniqueness
        Task<IEnumerable<Doctor>> GetBySpecialtyAsync(string specialty);
    }
}
