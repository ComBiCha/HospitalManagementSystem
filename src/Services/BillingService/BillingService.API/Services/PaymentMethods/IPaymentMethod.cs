using BillingService.API.Models;

namespace BillingService.API.Services.PaymentMethods
{
    public interface IPaymentMethod
    {
        string PaymentMethodName { get; }
        Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
        Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal amount);
    }

    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty;
        public int PatientId { get; set; }
        public int AppointmentId { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string? TransactionId { get; set; }
        public string? FailureReason { get; set; }
        public decimal Amount { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}
