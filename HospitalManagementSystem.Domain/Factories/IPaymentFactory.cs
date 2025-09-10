using HospitalManagementSystem.Domain.Payments;

namespace HospitalManagementSystem.Domain.Factories
{
    public interface IPaymentFactory
    {
        IPaymentMethod CreatePaymentMethod(string paymentMethodName);
        IEnumerable<string> GetAvailablePaymentMethods();
    }
}
