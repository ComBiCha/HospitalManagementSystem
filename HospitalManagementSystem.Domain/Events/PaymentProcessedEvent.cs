namespace HospitalManagementSystem.Domain.Events
{
    public class PaymentProcessedEvent
    {
        public int BillingId { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int ProcessedByUserId { get; set; }
        public string ProcessedByRole { get; set; } = string.Empty;
        public string? PaymentSource { get; set; }
        public string? SessionId { get; set; }
    }
}