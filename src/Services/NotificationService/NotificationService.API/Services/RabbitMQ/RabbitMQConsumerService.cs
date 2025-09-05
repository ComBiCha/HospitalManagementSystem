using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NotificationService.API.Data;
using NotificationService.API.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace NotificationService.API.Services.RabbitMQ
{
    public class RabbitMQConsumerService : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _exchangeName = "hospital.events";
        private readonly string _queueName = "appointment.notifications";

        public RabbitMQConsumerService(IConfiguration configuration, ILogger<RabbitMQConsumerService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

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

                // Declare queue
                _channel.QueueDeclare(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Bind queue to exchange with routing keys
                _channel.QueueBind(
                    queue: _queueName,
                    exchange: _exchangeName,
                    routingKey: "appointment.created");

                _channel.QueueBind(
                    queue: _queueName,
                    exchange: _exchangeName,
                    routingKey: "appointment.updated");

                _channel.QueueBind(
                    queue: _queueName,
                    exchange: _exchangeName,
                    routingKey: "appointment.cancelled");

                _logger.LogInformation("RabbitMQ Consumer Service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ Consumer Service");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting RabbitMQ Consumer Service");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation("Received message with routing key: {RoutingKey}", routingKey);
                    _logger.LogInformation("Message content: {Message}", message);

                    await ProcessMessage(routingKey, message);

                    // Acknowledge the message
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    // Reject and requeue the message
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("RabbitMQ Consumer Service is listening for messages...");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessMessage(string routingKey, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            try
            {
                var appointmentData = JsonConvert.DeserializeObject<AppointmentEventData>(message);
                if (appointmentData == null)
                {
                    _logger.LogWarning("Failed to deserialize appointment data");
                    return;
                }

                var notification = routingKey switch
                {
                    "appointment.created" => CreateAppointmentCreatedNotification(appointmentData),
                    "appointment.updated" => CreateAppointmentUpdatedNotification(appointmentData),
                    "appointment.cancelled" => CreateAppointmentCancelledNotification(appointmentData),
                    _ => null
                };

                if (notification != null)
                {
                    context.Notifications.Add(notification);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("📧 Email to Patient {PatientName}: {Content}", 
                        notification.PatientName, notification.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing appointment event");
                throw;
            }
        }

        private Notification CreateAppointmentCreatedNotification(AppointmentEventData data)
        {
            var appointmentDate = data.Date.ToString("yyyy-MM-dd HH:mm");
            var subject = "Appointment Confirmation";
            var content = $"Your appointment with Dr. {data.DoctorName} is scheduled for {appointmentDate}.";

            return new Notification
            {
                // New required properties
                UserId = data.PatientId.ToString(),
                Recipient = "patient@email.com", // In real app, get from patient data
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                
                // Legacy properties for backward compatibility
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = "patient@email.com",
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
                
                // Metadata as JSON
                Metadata = JsonConvert.SerializeObject(new
                {
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.created"
                })
            };
        }

        private Notification CreateAppointmentUpdatedNotification(AppointmentEventData data)
        {
            var appointmentDate = data.Date.ToString("yyyy-MM-dd HH:mm");
            var subject = "Appointment Updated";
            var content = $"Your appointment with Dr. {data.DoctorName} has been updated. New date: {appointmentDate}.";

            return new Notification
            {
                // New required properties
                UserId = data.PatientId.ToString(),
                Recipient = "patient@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                
                // Legacy properties for backward compatibility
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = "patient@email.com",
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
                
                // Metadata as JSON
                Metadata = JsonConvert.SerializeObject(new
                {
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.updated",
                    UpdatedAt = data.UpdatedAt
                })
            };
        }

        private Notification CreateAppointmentCancelledNotification(AppointmentEventData data)
        {
            var subject = "Appointment Cancelled";
            var content = $"Your appointment with Dr. {data.DoctorName} has been cancelled.";

            return new Notification
            {
                // New required properties
                UserId = data.PatientId.ToString(),
                Recipient = "patient@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                
                // Legacy properties for backward compatibility
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = "patient@email.com",
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
                
                // Metadata as JSON
                Metadata = JsonConvert.SerializeObject(new
                {
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.cancelled",
                    CancelledAt = data.CancelledAt
                })
            };
        }

        public override void Dispose()
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
                _logger.LogError(ex, "Error disposing RabbitMQ Consumer Service");
            }
            base.Dispose();
        }
    }

    public class AppointmentEventData
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialty { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime CancelledAt { get; set; }
    }
}