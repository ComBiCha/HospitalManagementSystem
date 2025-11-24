namespace HospitalManagementSystem.Domain.Entities
{
    public class Appointment
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "PendingPayment"; // PendingPayment, Scheduled, Completed, Cancelled, ExpiredPayment
        public AppointmentType Type { get; set; } = AppointmentType.InPerson;
        
        public decimal BookingFee { get; set; } = 50000; // Default 50k VND
        public int? BookingPaymentId { get; set; } // Link to Payment record
        
        public DateTime? PaymentExpiresAt { get; set; } 
        public string? CancellationReason { get; set; } 
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public MedicalRecord? MedicalRecord { get; set; } 
        public Payment? BookingPayment { get; set; }
    }

    public enum AppointmentStatus
    {
        Scheduled,
        Completed,
        Cancelled,
        Rescheduled
    }

    public enum AppointmentType
    {
        InPerson,
        Online
    }
}
