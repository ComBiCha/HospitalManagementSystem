using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Events;
using HospitalManagementSystem.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalManagementSystem.Infrastructure.RabbitMQ
{
    public class RabbitMQConsumerService : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPatientRepository _patientRepository;
        private readonly string _exchangeName = "hospital.events";
        private readonly string _queueName = "appointment.notifications";

        public RabbitMQConsumerService(IConfiguration configuration, ILogger<RabbitMQConsumerService> logger, IServiceProvider serviceProvider, IPatientRepository patientRepository)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _patientRepository = patientRepository;

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
            var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var patientRepository = scope.ServiceProvider.GetRequiredService<IPatientRepository>();

            try
            {
                object eventData = routingKey switch
                {
                    "appointment.created" => JsonConvert.DeserializeObject<AppointmentCreatedEvent>(message!),
                    "appointment.updated" => JsonConvert.DeserializeObject<AppointmentUpdatedEvent>(message!),
                    "appointment.cancelled" => JsonConvert.DeserializeObject<AppointmentCancelledEvent>(message!),
                    _ => null
                };

                if (eventData == null)
                {
                    _logger.LogWarning("Failed to deserialize appointment event data");
                    return;
                }

                Notification notification = routingKey switch
                {
                    "appointment.created" => CreateAppointmentCreatedNotification((AppointmentCreatedEvent)eventData!, patientRepository),
                    "appointment.updated" => CreateAppointmentUpdatedNotification((AppointmentUpdatedEvent)eventData!, patientRepository),
                    "appointment.cancelled" => CreateAppointmentCancelledNotification((AppointmentCancelledEvent)eventData!, patientRepository),
                    _ => null
                };

                if (notification != null)
                {
                    context.Notifications.Add(notification);
                    await context.SaveChangesAsync();

                    var emailChannel = scope.ServiceProvider.GetServices<INotificationChannel>()
                        .FirstOrDefault(c => c.ChannelType == "Email");

                    if (emailChannel != null)
                    {
                        await emailChannel.SendAsync(new NotificationMessage
                        {
                            Recipient = notification.Recipient,
                            Subject = notification.Subject,
                            Content = notification.Content
                        });
                    }
                    else
                    {
                        _logger.LogWarning("No EmailNotificationChannel found in DI");
                    }

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

        private Notification CreateAppointmentCreatedNotification(AppointmentCreatedEvent data, IPatientRepository patientRepository)
        {
            var patient = patientRepository.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";

            var appointmentDate = data.Date.ToString("yyyy-MM-dd HH:mm");
            var subject = "Appointment Confirmation";
            var content = $"Your appointment with Dr. {data.DoctorName} is scheduled for {appointmentDate}.";

            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = recipientEmail,
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.created"
                })
            };
        }

        private Notification CreateAppointmentUpdatedNotification(AppointmentUpdatedEvent data, IPatientRepository patientRepository)
        {
            var patient = patientRepository.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";

            var appointmentDate = data.Date.ToString("yyyy-MM-dd HH:mm");
            var subject = "Appointment Updated";
            var content = $"Your appointment with Dr. {data.DoctorName} has been updated. New date: {appointmentDate}.";

            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = recipientEmail,
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.updated",
                    UpdatedAt = data.UpdatedAt
                })
            };
        }

        private Notification CreateAppointmentCancelledNotification(AppointmentCancelledEvent data, IPatientRepository patientRepository)
        {
            var patient = patientRepository.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";

            var subject = "Appointment Cancelled";
            var content = $"Your appointment with Dr. {data.DoctorName} has been cancelled.";

            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                AppointmentId = data.AppointmentId,
                Message = content,
                Type = "Email",
                RecipientEmail = recipientEmail,
                PatientName = data.PatientName,
                DoctorName = data.DoctorName,
                IsRead = false,
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
}