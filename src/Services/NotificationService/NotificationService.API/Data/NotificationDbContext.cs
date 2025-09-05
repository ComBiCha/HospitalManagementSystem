using Microsoft.EntityFrameworkCore;
using NotificationService.API.Models;

namespace NotificationService.API.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }

        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}
