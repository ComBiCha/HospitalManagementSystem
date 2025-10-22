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
    }
}
