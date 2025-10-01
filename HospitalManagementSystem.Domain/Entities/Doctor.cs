using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    [Table("Doctors")]
    public class Doctor
    {
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Column("Specialty")]
        public string Specialty { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Column("Status")]
        public DoctorStatus Status { get; set; } = DoctorStatus.Active;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Helper methods for status management
        public bool HasStatus(DoctorStatus status) => Status.HasFlag(status);

        public void AddStatus(DoctorStatus status) => Status |= status;

        public void RemoveStatus(DoctorStatus status) => Status &= ~status;

        // Convenient status properties
        public bool IsActive => HasStatus(DoctorStatus.Active);
        public bool IsOnDuty => HasStatus(DoctorStatus.OnDuty);
        public bool IsOffDuty => HasStatus(DoctorStatus.OffDuty);
        public bool IsOnLeave => HasStatus(DoctorStatus.OnLeave);
        public bool IsOnCall => HasStatus(DoctorStatus.OnCall);
        public bool IsInSurgery => HasStatus(DoctorStatus.InSurgery);
        public bool IsOnVacation => HasStatus(DoctorStatus.OnVacation);
    }
}