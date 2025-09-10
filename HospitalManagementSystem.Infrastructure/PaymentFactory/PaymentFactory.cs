using HospitalManagementSystem.Infrastructure.PaymentMethods;
using HospitalManagementSystem.Domain.Factories;
using HospitalManagementSystem.Domain.Payments;

namespace HospitalManagementSystem.Infrastructure.PaymentFactory
{
    public class PaymentFactory : IPaymentFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _paymentMethods;

        public PaymentFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _paymentMethods = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stripe"] = typeof(StripePaymentMethod),
                ["Cash"] = typeof(CashPaymentMethod)
            };
        }

        public IPaymentMethod CreatePaymentMethod(string paymentMethodName)
        {
            if (string.IsNullOrWhiteSpace(paymentMethodName))
            {
                throw new ArgumentException("Payment method name cannot be null or empty", nameof(paymentMethodName));
            }

            if (!_paymentMethods.TryGetValue(paymentMethodName, out var paymentMethodType))
            {
                throw new NotSupportedException($"Payment method '{paymentMethodName}' is not supported. Available methods: {string.Join(", ", GetAvailablePaymentMethods())}");
            }

            var paymentMethod = _serviceProvider.GetService(paymentMethodType) as IPaymentMethod;
            
            if (paymentMethod == null)
            {
                throw new InvalidOperationException($"Failed to create payment method '{paymentMethodName}'. Make sure it's registered in DI container.");
            }

            return paymentMethod;
        }

        public IEnumerable<string> GetAvailablePaymentMethods()
        {
            return _paymentMethods.Keys;
        }
    }
}
