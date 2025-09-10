using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;

namespace HospitalManagementSystem.Domain.Strategies
{
    public interface IBillingStrategy
    {
        Task<decimal> CalculateTotalAmountAsync(Billing billing);
        Task<string> GenerateInvoiceNumberAsync(Billing billing);
        Task ValidateBillingAsync(Billing billing);
        string GetBillingType();
    }
}