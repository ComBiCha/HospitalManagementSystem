namespace BillingService.API.Services.RabbitMQ
{
    public interface IRabbitMQService
    {
        Task PublishPaymentInitiatedAsync(object paymentData);
        Task PublishPaymentProcessedAsync(object paymentData);
        Task PublishPaymentFailedAsync(object paymentData);
        Task PublishRefundProcessedAsync(object refundData);
    }
}
