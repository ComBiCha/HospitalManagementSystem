using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Infrastructure.Persistence
{
    public class HospitalDbContext : DbContext
    {
        public HospitalDbContext(DbContextOptions<HospitalDbContext> options) : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Billing> Billings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ImageInfo> Images { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<PatientIdentifiers> PatientIdentifiers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Email).IsRequired();

                // Quan hệ 1-n với PatientIdentifiers
                entity.HasMany(e => e.PatientIdentifiers)
                      .WithOne(pi => pi.Patient)
                      .HasForeignKey(pi => pi.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PatientIdentifiers>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EHRSystem).IsRequired();
                entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IdentifierType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired(false);

                entity.HasIndex(e => e.PatientId);
                entity.HasIndex(e => new { e.EHRSystem, e.ExternalId });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.HasMany<RefreshToken>()
                    .WithOne(rt => rt.User)
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken entity configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.IsRevoked).IsRequired();
            });

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Specialty).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<ImageInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ImageType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.MinioObjectKey).IsRequired().HasMaxLength(500);
                
                entity.HasIndex(e => e.AppointmentId);
                entity.HasIndex(e => e.PatientId);
                entity.HasIndex(e => e.DoctorId);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Required properties
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Recipient).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.ChannelType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
                
                // Optional properties
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.Metadata).HasColumnType("jsonb"); // PostgreSQL JSON column
                
                // New properties for multi-channel support
                entity.Property(e => e.AppointmentDate);
                entity.Property(e => e.SentAt);
                
                // Legacy properties for backward compatibility
                entity.Property(e => e.AppointmentId);
                entity.Property(e => e.PatientName).HasMaxLength(255);
                entity.Property(e => e.DoctorName).HasMaxLength(255);
                entity.Property(e => e.RecipientEmail).HasMaxLength(255);
                entity.Property(e => e.Type).HasMaxLength(50);
                entity.Property(e => e.Message);
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                
                // Default values
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.RetryCount).HasDefaultValue(0);
                entity.Property(e => e.Status).HasDefaultValue(NotificationStatus.Pending);

                // Indexes for performance
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Notifications_UserId");
                entity.HasIndex(e => e.Recipient).HasDatabaseName("IX_Notifications_Recipient");
                entity.HasIndex(e => e.ChannelType).HasDatabaseName("IX_Notifications_ChannelType");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_Notifications_Status");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_Notifications_CreatedAt");
                entity.HasIndex(e => new { e.Status, e.RetryCount }).HasDatabaseName("IX_Notifications_Status_RetryCount");
                
                // Additional indexes for appointment-related queries
                entity.HasIndex(e => e.AppointmentId).HasDatabaseName("IX_Notifications_AppointmentId");
                entity.HasIndex(e => e.AppointmentDate).HasDatabaseName("IX_Notifications_AppointmentDate");
                entity.HasIndex(e => e.IsRead).HasDatabaseName("IX_Notifications_IsRead");
                
                // Composite indexes for common query patterns
                entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("IX_Notifications_UserId_Status");
                entity.HasIndex(e => new { e.ChannelType, e.Status }).HasDatabaseName("IX_Notifications_ChannelType_Status");
                entity.HasIndex(e => new { e.AppointmentId, e.Status }).HasDatabaseName("IX_Notifications_AppointmentId_Status");
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PatientId).IsRequired();
                entity.Property(e => e.DoctorId).IsRequired();
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                
                // Index for performance
                entity.HasIndex(e => e.PatientId);
                entity.HasIndex(e => e.DoctorId);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<Billing>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.PaymentMethod).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.HasIndex(e => e.AppointmentId);
                entity.HasIndex(e => e.PatientId);
                entity.HasIndex(e => e.TransactionId);
            });

            // Seed default users
            SeedDefaultUsers(modelBuilder);
        }

        private static void SeedDefaultUsers(ModelBuilder modelBuilder)
        {
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
            var doctorPasswordHash = BCrypt.Net.BCrypt.HashPassword("doctor123");
            var patientPasswordHash = BCrypt.Net.BCrypt.HashPassword("patient123");

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    Email = "admin@hospital.com",
                    PasswordHash = adminPasswordHash,
                    FirstName = "System",
                    LastName = "Administrator",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
