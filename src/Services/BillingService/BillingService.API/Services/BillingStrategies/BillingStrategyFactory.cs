namespace BillingService.API.Services.BillingStrategies
{
    public class BillingStrategyFactory : IBillingStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _billingStrategies;

        public BillingStrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _billingStrategies = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["Insurance"] = typeof(InsuranceBillingStrategy)
                // ["SelfPay"] = typeof(SelfPayBillingStrategy),
                // ["Corporate"] = typeof(CorporateBillingStrategy)
            };
        }

        public IBillingStrategy CreateBillingStrategy(string billingType)
        {
            if (string.IsNullOrWhiteSpace(billingType))
            {
                throw new ArgumentException("Billing type cannot be null or empty", nameof(billingType));
            }

            if (!_billingStrategies.TryGetValue(billingType, out var strategyType))
            {
                throw new NotSupportedException($"Billing type '{billingType}' is not supported. Available types: {string.Join(", ", GetAvailableBillingTypes())}");
            }

            var strategy = _serviceProvider.GetService(strategyType) as IBillingStrategy;
            
            if (strategy == null)
            {
                throw new InvalidOperationException($"Failed to create billing strategy '{billingType}'. Make sure it's registered in DI container.");
            }

            return strategy;
        }

        public IEnumerable<string> GetAvailableBillingTypes()
        {
            return _billingStrategies.Keys;
        }
    }
}