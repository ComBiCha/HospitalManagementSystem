using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    [Table("MedicalRecords")]
    public class MedicalRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        // Diagnosis information - stored as JSON arrays/strings
        [Required]
        public string Diagnosis { get; set; } = "[]"; // JSON array from diagnoses.json

        public string Symptoms { get; set; } = "[]"; // JSON array from symptoms.json

        public string Treatment { get; set; } = string.Empty; // Free text

        public string Prescription { get; set; } = "[]"; // JSON array from drugs.json + medical_tests.json

        public string Notes { get; set; } = string.Empty; // Free text

        // Medical costs
        [Required]
        [Range(0, double.MaxValue)]
        public decimal ConsultationFee { get; set; } = 200000; // Phí khám: 200k

        [Range(0, double.MaxValue)]
        public decimal MedicineFee { get; set; } = 0; // Phí thuốc

        [Range(0, double.MaxValue)]
        public decimal TestFee { get; set; } = 0; // Phí xét nghiệm

        [Range(0, double.MaxValue)]
        public decimal OtherFee { get; set; } = 0; // Phí khác

        // Computed total
        [NotMapped]
        public decimal TotalFee => ConsultationFee + MedicineFee + TestFee + OtherFee;

        // Payment status
        public decimal PaidAmount { get; set; } = 0; // Số tiền đã thanh toán (bao gồm tạm ứng)
        
        [NotMapped]
        public decimal RemainingAmount => TotalFee - PaidAmount; // Còn nợ
        
        [NotMapped]
        public bool IsFullyPaid => PaidAmount >= TotalFee;

        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, FullyPaid

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public Appointment Appointment { get; set; } = null!;
        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public ICollection<Payment> Payments { get; set; } = new List<Payment>(); // Multiple payments
    }
}