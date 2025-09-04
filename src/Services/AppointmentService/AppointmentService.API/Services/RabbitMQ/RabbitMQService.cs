using RabbitMQ.Client;
using Newtonsoft.Json;
using System.Text;

namespace AppointmentService.API.Services.RabbitMQ
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly string _exchangeName = "hospital.events";

        public RabbitMQService(ILogger<RabbitMQService> logger, IConfiguration configuration)
        {
            _logger = logger;
            
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = configuration.GetValue<string>("RabbitMQ:HostName") ?? "localhost",
                    Port = configuration.GetValue<int>("RabbitMQ:Port", 5672),
                    UserName = configuration.GetValue<string>("RabbitMQ:UserName") ?? "guest",
                    Password = configuration.GetValue<string>("RabbitMQ:Password") ?? "guest"
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange
                _channel.ExchangeDeclare(
                    exchange: _exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task PublishAppointmentCreatedAsync(object appointmentData)
        {
            await PublishEventAsync("appointment.created", appointmentData);
        }

        public async Task PublishAppointmentUpdatedAsync(object appointmentData)
        {
            await PublishEventAsync("appointment.updated", appointmentData);
        }

        public async Task PublishAppointmentCancelledAsync(object appointmentData)
        {
            await PublishEventAsync("appointment.cancelled", appointmentData);
        }

        private async Task PublishEventAsync(string routingKey, object data)
        {
            try
            {
                var message = JsonConvert.SerializeObject(data);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.MessageId = Guid.NewGuid().ToString();

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published event {RoutingKey} with message: {Message}", routingKey, message);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event {RoutingKey}", routingKey);
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
