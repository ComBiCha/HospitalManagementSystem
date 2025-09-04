using BillingService.API.Models;

namespace BillingService.API.Services.BillingStrategies
{
    public class InsuranceBillingStrategy : IBillingStrategy
    {
        public string GetBillingType() => "Insurance";

        public async Task<decimal> CalculateTotalAmountAsync(Billing billing)
        {
            // Insurance billing logic: apply coverage percentage
            var coveragePercentage = 0.8m; // 80% coverage
            var totalAmount = billing.Amount * (1 - coveragePercentage);
            
            // Add insurance processing fee
            var processingFee = 25.00m;
            
            return await Task.FromResult(totalAmount + processingFee);
        }

        public async Task<string> GenerateInvoiceNumberAsync(Billing billing)
        {
            var prefix = "INS";
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            return await Task.FromResult($"{prefix}-{timestamp}-{billing.PatientId}");
        }

        public async Task ValidateBillingAsync(Billing billing)
        {
            // Insurance specific validations
            if (string.IsNullOrWhiteSpace(billing.InsuranceNumber))
            {
                throw new InvalidOperationException("Insurance number is required for insurance billing");
            }

            if (billing.Amount <= 0)
            {
                throw new InvalidOperationException("Billing amount must be greater than zero");
            }

            // Validate insurance coverage
            // In real scenario, you would call insurance API to validate
            await Task.CompletedTask;
        }
    }
}