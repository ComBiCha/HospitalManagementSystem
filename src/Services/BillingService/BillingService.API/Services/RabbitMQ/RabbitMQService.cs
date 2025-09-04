using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BillingService.API.Services.RabbitMQ
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;

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

            // Declare exchanges
            _channel.ExchangeDeclare("billing.events", ExchangeType.Topic, true);
        }

        public async Task PublishPaymentProcessedAsync(object paymentData)
        {
            await PublishEventAsync("payment.processed", paymentData);
        }

        public async Task PublishPaymentFailedAsync(object paymentData)
        {
            await PublishEventAsync("payment.failed", paymentData);
        }

        public async Task PublishRefundProcessedAsync(object refundData)
        {
            await PublishEventAsync("refund.processed", refundData);
        }

        public async Task PublishPaymentInitiatedAsync(object paymentData)
        {
            await PublishEventAsync("payment.initiated", paymentData);
            _logger.LogInformation("Published payment initiated event");
        }

        private async Task PublishEventAsync(string routingKey, object data)
        {
            try
            {
                var message = JsonSerializer.Serialize(data);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    exchange: "billing.events",
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published event {RoutingKey} to RabbitMQ", routingKey);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {RoutingKey} to RabbitMQ", routingKey);
                throw;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
