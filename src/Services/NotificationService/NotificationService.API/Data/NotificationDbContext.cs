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
                entity.Property(e => e.AppointmentId).IsRequired();
                entity.Property(e => e.Message).IsRequired();
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PatientName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.DoctorName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.IsRead).IsRequired();

                // Indexes for performance
                entity.HasIndex(e => e.AppointmentId);
                entity.HasIndex(e => e.RecipientEmail);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsRead);
            });
        }
    }
}
