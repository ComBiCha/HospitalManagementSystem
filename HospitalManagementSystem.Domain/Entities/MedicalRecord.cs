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

        // Diagnosis information
        [Required]
        [StringLength(500)]
        public string Diagnosis { get; set; } = string.Empty; // Chẩn đoán

        [StringLength(2000)]
        public string? Symptoms { get; set; } // Triệu chứng

        [StringLength(2000)]
        public string? Treatment { get; set; } // Điều trị

        [StringLength(2000)]
        public string? Prescription { get; set; } // Đơn thuốc

        [StringLength(2000)]
        public string? Notes { get; set; } // Ghi chú

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
        public decimal TotalFee => ConsultationFee + MedicineFee + TestFee + OtherFee;

        // Payment status
        public decimal PaidAmount { get; set; } = 0; // Số tiền đã thanh toán (bao gồm tạm ứng)
        public decimal RemainingAmount => TotalFee - PaidAmount; // Còn nợ
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