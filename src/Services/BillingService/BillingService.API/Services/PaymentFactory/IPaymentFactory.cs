using BillingService.API.Services.PaymentMethods;

namespace BillingService.API.Services.PaymentFactory
{
    public interface IPaymentFactory
    {
        IPaymentMethod CreatePaymentMethod(string paymentMethodName);
        IEnumerable<string> GetAvailablePaymentMethods();
    }
}
