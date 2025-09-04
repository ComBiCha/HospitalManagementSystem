using BillingService.API.Models;

namespace BillingService.API.Services.BillingStrategies
{
    public interface IBillingStrategy
    {
        Task<decimal> CalculateTotalAmountAsync(Billing billing);
        Task<string> GenerateInvoiceNumberAsync(Billing billing);
        Task ValidateBillingAsync(Billing billing);
        string GetBillingType();
    }
}