using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    [Table("DoctorShifts")]
    public class DoctorShift
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("DoctorId")]
        public int DoctorId { get; set; }

        [Required]
        [Column("DayOfWeek")]
        public DayOfWeek DayOfWeek { get; set; } // Monday, Tuesday, ...

        [Required]
        [Column("StartTime")]
        public TimeSpan StartTime { get; set; } // e.g., 08:00

        [Required]
        [Column("EndTime")]
        public TimeSpan EndTime { get; set; } // e.g., 17:00

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public Doctor Doctor { get; set; } = null!;
    }
}