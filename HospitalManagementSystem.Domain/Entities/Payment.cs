using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    [Table("Payments")]
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        // Payment can be for Appointment (booking fee) OR MedicalRecord (treatment fee)
        public int? AppointmentId { get; set; } // For booking fee
        public int? MedicalRecordId { get; set; } // For treatment fee

        [Required]
        [StringLength(50)]
        public string PaymentType { get; set; } = string.Empty; // "BookingFee", "TreatmentFee", "Deposit", "FinalPayment"

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty; // "Stripe", "Cash", "BankTransfer"

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? TransactionId { get; set; } // For online payment

        [StringLength(200)]
        public string? InvoiceNumber { get; set; }

        [StringLength(500)]
        public string? FailureReason { get; set; }

        // Stripe-specific
        [StringLength(500)]
        public string? StripeSessionId { get; set; }
        
        [StringLength(500)]
        public string? StripePaymentIntentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public Patient Patient { get; set; } = null!;
        public Appointment? Appointment { get; set; } // Nullable
        public MedicalRecord? MedicalRecord { get; set; } // Nullable
    }

    // Payment type constants
    public static class PaymentTypes
    {
        public const string BookingFee = "BookingFee"; // Phí đặt lịch
        public const string Deposit = "Deposit"; // Tạm ứng
        public const string TreatmentFee = "TreatmentFee"; // Viện phí
        public const string FinalPayment = "FinalPayment"; // Thanh toán cuối
    }

    // Payment status constants
    public static class PaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }
}