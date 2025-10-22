using HospitalManagementSystem.Domain.Payments;
using Stripe.Checkout;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace HospitalManagementSystem.Infrastructure.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        public Task<Session> CreateCheckoutSessionAsync(
            long amount,
            string productName,
            string productDescription,
            string customerEmail,
            string successUrl,
            string cancelUrl,
            Dictionary<string, string> metadata)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "vnd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = productName,
                                Description = productDescription,
                            },
                            UnitAmount = amount,
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = customerEmail,
                Metadata = metadata
            };

            var service = new SessionService();
            return service.CreateAsync(options);
        }
    }
}
