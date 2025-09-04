namespace BillingService.API.Services.BillingStrategies
{
    public interface IBillingStrategyFactory
    {
        IBillingStrategy CreateBillingStrategy(string billingType);
        IEnumerable<string> GetAvailableBillingTypes();
    }
}