namespace HospitalManagementSystem.Domain.Events
{
    public class PaymentFailedEvent
    {
        public int BillingId { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public DateTime FailedAt { get; set; }
        public int ProcessedByUserId { get; set; }
        public string ProcessedByRole { get; set; } = string.Empty;
    }
}