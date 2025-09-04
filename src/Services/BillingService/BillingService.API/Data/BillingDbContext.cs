using Microsoft.EntityFrameworkCore;
using BillingService.API.Models;

namespace BillingService.API.Data
{
    public class BillingDbContext : DbContext
    {
        public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options)
        {
        }

        public DbSet<Billing> Billings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}
