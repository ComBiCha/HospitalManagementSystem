using HospitalManagementSystem.Domain.Payments;
using Microsoft.Extensions.Logging;


namespace HospitalManagementSystem.Infrastructure.PaymentMethods
{
    public class CashPaymentMethod : IPaymentMethod
    {
        public string PaymentMethodName => "Cash";
        
        private readonly ILogger<CashPaymentMethod> _logger;

        public CashPaymentMethod(ILogger<CashPaymentMethod> logger)
        {
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing cash payment for amount: {Amount}, PatientId: {PatientId}", 
                    request.Amount, request.PatientId);

                // Simulate cash payment processing
                await Task.Delay(100); // Simulate processing time

                var transactionId = $"CASH_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

                _logger.LogInformation("Cash payment processed with transaction ID: {TransactionId}", transactionId);

                return new PaymentResult
                {
                    IsSuccess = true,
                    TransactionId = transactionId,
                    Amount = request.Amount,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["payment_method"] = "Cash",
                        ["processed_at"] = DateTime.UtcNow
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing cash payment for PatientId: {PatientId}", request.PatientId);
                
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = "Internal cash payment processing error",
                    Amount = request.Amount
                };
            }
        }

        public async Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal amount)
        {
            try
            {
                _logger.LogInformation("Processing cash refund for transaction: {TransactionId}, amount: {Amount}", 
                    transactionId, amount);

                // Simulate cash refund processing
                await Task.Delay(100);

                var refundTransactionId = $"CASH_REFUND_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

                _logger.LogInformation("Cash refund processed with transaction ID: {RefundTransactionId}", refundTransactionId);

                return new PaymentResult
                {
                    IsSuccess = true,
                    TransactionId = refundTransactionId,
                    Amount = amount,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["refund_transaction_id"] = refundTransactionId,
                        ["original_transaction_id"] = transactionId,
                        ["refund_method"] = "Cash"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing cash refund for transaction: {TransactionId}", transactionId);
                
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = "Internal cash refund processing error",
                    Amount = amount
                };
            }
        }
    }
}
