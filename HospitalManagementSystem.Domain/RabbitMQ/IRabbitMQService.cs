using HospitalManagementSystem.Domain.Events;

namespace HospitalManagementSystem.Domain.RabbitMQ
{
    public interface IRabbitMQService
    {
        Task PublishAppointmentCreatedAsync(AppointmentCreatedEvent appointmentData);
        Task PublishAppointmentUpdatedAsync(AppointmentUpdatedEvent appointmentData);
        Task PublishAppointmentCancelledAsync(AppointmentCancelledEvent appointmentData);
        Task PublishPaymentInitiatedAsync(PaymentInitiatedEvent paymentData);
        Task PublishPaymentProcessedAsync(PaymentProcessedEvent paymentData);
        Task PublishPaymentFailedAsync(PaymentFailedEvent paymentData);
        Task PublishRefundProcessedAsync(RefundProcessedEvent refundData);
    }
}
