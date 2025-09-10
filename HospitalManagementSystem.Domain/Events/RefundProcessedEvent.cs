namespace HospitalManagementSystem.Domain.Events
{
    public class RefundProcessedEvent
    {
        public int BillingId { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? OriginalTransactionId { get; set; }
        public string? RefundTransactionId { get; set; }
        public DateTime RefundedAt { get; set; }
        public int RefundedByUserId { get; set; }
        public string RefundedByRole { get; set; } = string.Empty;
    }
}