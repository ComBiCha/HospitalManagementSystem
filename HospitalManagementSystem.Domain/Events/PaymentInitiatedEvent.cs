namespace HospitalManagementSystem.Domain.Events
{
    public class PaymentInitiatedEvent
    {
        public int BillingId { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public DateTime InitiatedAt { get; set; }
        public string? CheckoutUrl { get; set; }
    }
}