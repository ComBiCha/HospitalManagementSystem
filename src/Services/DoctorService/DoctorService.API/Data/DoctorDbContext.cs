using Microsoft.EntityFrameworkCore;
using DoctorService.API.Models;

namespace DoctorService.API.Data
{
    public class DoctorDbContext : DbContext
    {
        public DoctorDbContext(DbContextOptions<DoctorDbContext> options) : base(options)
        {
        }

        public DbSet<Doctor> Doctors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Doctor entity configuration
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Specialty).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);  // ✅ Email configuration
                
                // ✅ Add unique index for Email
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Seed default doctors
            SeedDefaultDoctors(modelBuilder);
        }

        private static void SeedDefaultDoctors(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Doctor>().HasData(
                new Doctor
                {
                    Id = 1,
                    Name = "Dr. Sarah Johnson",
                    Specialty = "Cardiology",
                    Email = "doctor1@hospital.com"  // ✅ Added Email
                },
                new Doctor
                {
                    Id = 2,
                    Name = "Dr. Michael Chen",
                    Specialty = "Pediatrics", 
                    Email = "doctor2@hospital.com"  // ✅ Added Email
                },
                new Doctor
                {
                    Id = 3,
                    Name = "Dr. Emily Davis",
                    Specialty = "Orthopedics",
                    Email = "doctor3@hospital.com"  // ✅ Added Email
                }
            );
        }
    }
}
