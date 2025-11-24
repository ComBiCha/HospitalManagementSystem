using Stripe;
using Stripe.Checkout;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HospitalManagementSystem.Domain.Payments
{
    public interface IStripePaymentService
    {
        Task<Session> CreateCheckoutSessionAsync(
            long amount,
            string productName,
            string productDescription,
            string customerEmail,
            string successUrl,
            string cancelUrl,
            Dictionary<string, string> metadata);

        Task<PaymentIntent> CreatePaymentIntentAsync(
            long amount,
            string currency,
            string description,
            Dictionary<string, string> metadata);

        Task<Refund> RefundPaymentAsync(string paymentIntentId, long amount, string reason);

        Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId);
    }
}
