namespace HospitalManagementSystem.Domain.Strategies
{
    public interface IBillingStrategyFactory
    {
        IBillingStrategy CreateBillingStrategy(string billingType);
        IEnumerable<string> GetAvailableBillingTypes();
    }
}