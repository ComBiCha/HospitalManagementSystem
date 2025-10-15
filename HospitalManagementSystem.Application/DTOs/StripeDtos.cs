namespace HospitalManagementSystem.Application.DTOs
{
    public class InitiateStripePaymentResponseDto
    {
        public int PaymentId { get; set; }
        public string StripeCheckoutUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
