using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HospitalManagementSystem.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HospitalDbContext>
{
    public HospitalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HospitalDbContext>();
        
        // Use a connection string for design-time (migrations) with SSL disabled
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=HMS_CentralDB;Username=postgres;Password=postgres123;SSL Mode=Disable");

        return new HospitalDbContext(optionsBuilder.Options);
    }
}