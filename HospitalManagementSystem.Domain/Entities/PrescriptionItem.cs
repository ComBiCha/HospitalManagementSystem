using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{ 
    [Table("PrescriptionItems")]
    public class PrescriptionItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MedicalRecordId { get; set; }

        [Required]
        public string ItemType { get; set; } = "Medicine"; // "Medicine", "Test"

        [Required]
        public string ItemCode { get; set; } = string.Empty; 

        [Required]
        public string ItemName { get; set; } = string.Empty;

        public int? Quantity { get; set; } // Chỉ cho thuốc
        public string? Unit { get; set; } // Chỉ cho Test

        public decimal Price { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, CancelRequested, Cancelled

        // Hủy
        public bool IsCancelRequested { get; set; } = false;
        public string? CancelReason { get; set; }
        public DateTime? CancelRequestedAt { get; set; }
        public int? CancelRequestedByDoctorId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(MedicalRecordId))]
        public MedicalRecord MedicalRecord { get; set; } = null!;
    }
}
