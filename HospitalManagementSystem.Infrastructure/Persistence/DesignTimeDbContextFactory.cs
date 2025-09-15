using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HospitalManagementSystem.Infrastructure.Persistence
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HospitalDbContext>
    {
        public HospitalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<HospitalDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=HMS_CentralDB;Username=postgres;Password=postgres123"); // lấy từ .env hoặc appsettings nếu muốn

            return new HospitalDbContext(optionsBuilder.Options);
        }
    }
}