using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{ 
    [Table("MedicalRecordHistories")]
    public class MedicalRecordHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MedicalRecordId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        public string Diagnosis { get; set; } = "[]";
        public string Symptoms { get; set; } = "[]";
        public string Treatment { get; set; } = string.Empty;
        public string Prescription { get; set; } = "[]"; 
        public string Notes { get; set; } = string.Empty;

        public decimal MedicineFee { get; set; }
        public decimal TestFee { get; set; }
        public decimal OtherFee { get; set; }

        public string Action { get; set; } = "Update"; // "Create", "Update", "Complete"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(MedicalRecordId))]
        public MedicalRecord MedicalRecord { get; set; } = null!;
    }
}
