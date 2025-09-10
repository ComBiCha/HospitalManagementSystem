using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using HospitalManagementSystem.Domain.Events;
using HospitalManagementSystem.Domain.RabbitMQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace HospitalManagementSystem.Infrastructure.RabbitMQ
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly string _hospitalExchange;
        private readonly string _billingExchange;

        public RabbitMQService(ILogger<RabbitMQService> logger, IConfiguration configuration)
        {
            _logger = logger;
            var factory = new ConnectionFactory()
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _hospitalExchange = configuration["RabbitMQ:HospitalExchange"] ?? "hospital.events";
            _billingExchange = configuration["RabbitMQ:BillingExchange"] ?? "billing.events";

            _channel.ExchangeDeclare(_hospitalExchange, ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.ExchangeDeclare(_billingExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        }

        // Appointment events
        public async Task PublishAppointmentCreatedAsync(AppointmentCreatedEvent appointmentData)
            => await PublishEventAsync(_hospitalExchange, "appointment.created", appointmentData);

        public async Task PublishAppointmentUpdatedAsync(AppointmentUpdatedEvent appointmentData)
            => await PublishEventAsync(_hospitalExchange, "appointment.updated", appointmentData);

        public async Task PublishAppointmentCancelledAsync(AppointmentCancelledEvent appointmentData)
            => await PublishEventAsync(_hospitalExchange, "appointment.cancelled", appointmentData);

        // Billing events
        public async Task PublishPaymentInitiatedAsync(PaymentInitiatedEvent paymentData)
            => await PublishEventAsync(_billingExchange, "payment.initiated", paymentData);

        public async Task PublishPaymentProcessedAsync(PaymentProcessedEvent paymentData)
            => await PublishEventAsync(_billingExchange, "payment.processed", paymentData);

        public async Task PublishPaymentFailedAsync(PaymentFailedEvent paymentData)
            => await PublishEventAsync(_billingExchange, "payment.failed", paymentData);

        public async Task PublishRefundProcessedAsync(RefundProcessedEvent refundData)
            => await PublishEventAsync(_billingExchange, "refund.processed", refundData);

        private async Task PublishEventAsync(string exchange, string routingKey, object data)
        {
            try
            {
                var message = JsonSerializer.Serialize(data);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.MessageId = Guid.NewGuid().ToString();

                _channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published event {RoutingKey} to {Exchange}: {Message}", routingKey, exchange, message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {RoutingKey} to {Exchange}", routingKey, exchange);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ connection");
            }
        }
    }
}
