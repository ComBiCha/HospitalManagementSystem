using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Events;
using HospitalManagementSystem.Domain.Notifications;
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
        private readonly string _exchangeName = "hospital.events";
        private readonly string _queueName = "appointment.notifications";
        private readonly string _billingExchangeName = "billing.events";
        private readonly string _billingQueueName = "billing.notifications";

        public RabbitMQConsumerService(IConfiguration configuration, ILogger<RabbitMQConsumerService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            var factory = new ConnectionFactory()
            {
                HostName = configuration.GetValue<string>("RabbitMQ:HostName") ?? "localhost",
                Port = configuration.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = configuration.GetValue<string>("RabbitMQ:UserName") ?? "guest",
                Password = configuration.GetValue<string>("RabbitMQ:Password") ?? "guest"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Hospital exchange/queue/routing
            _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(_queueName, _exchangeName, "appointment.created");
            _channel.QueueBind(_queueName, _exchangeName, "appointment.updated");
            _channel.QueueBind(_queueName, _exchangeName, "appointment.cancelled");

            // Billing exchange/queue/routing
            _channel.ExchangeDeclare(_billingExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.QueueDeclare(_billingQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(_billingQueueName, _billingExchangeName, "payment.initiated");
            _channel.QueueBind(_billingQueueName, _billingExchangeName, "payment.processed");
            _channel.QueueBind(_billingQueueName, _billingExchangeName, "payment.failed");
            _channel.QueueBind(_billingQueueName, _billingExchangeName, "refund.processed");

            _logger.LogInformation("RabbitMQ Consumer Service initialized for hospital and billing events");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Appointment consumer
            var appointmentConsumer = new EventingBasicConsumer(_channel);
            appointmentConsumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation("Received appointment message: {RoutingKey}", routingKey);
                    await ProcessAppointmentMessage(routingKey, message);

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing appointment message");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };
            _channel.BasicConsume(_queueName, autoAck: false, consumer: appointmentConsumer);

            // Billing consumer
            var billingConsumer = new EventingBasicConsumer(_channel);
            billingConsumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation("Received billing message: {RoutingKey}", routingKey);
                    await ProcessBillingMessage(routingKey, message);

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing billing message");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };
            _channel.BasicConsume(_billingQueueName, autoAck: false, consumer: billingConsumer);

            // Keep service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessAppointmentMessage(string routingKey, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var patientRepository = scope.ServiceProvider.GetRequiredService<IPatientRepository>();

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

        private async Task ProcessBillingMessage(string routingKey, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();

            object eventData = routingKey switch
            {
                "payment.initiated" => JsonConvert.DeserializeObject<PaymentInitiatedEvent>(message!),
                "payment.processed" => JsonConvert.DeserializeObject<PaymentProcessedEvent>(message!),
                "payment.failed" => JsonConvert.DeserializeObject<PaymentFailedEvent>(message!),
                "refund.processed" => JsonConvert.DeserializeObject<RefundProcessedEvent>(message!),
                _ => null
            };

            if (eventData == null)
            {
                _logger.LogWarning("Failed to deserialize billing event data");
                return;
            }

            Notification notification = routingKey switch
            {
                "payment.initiated" => CreatePaymentInitiatedNotification((PaymentInitiatedEvent)eventData!),
                "payment.processed" => CreatePaymentProcessedNotification((PaymentProcessedEvent)eventData!),
                "payment.failed" => CreatePaymentFailedNotification((PaymentFailedEvent)eventData!),
                "refund.processed" => CreateRefundProcessedNotification((RefundProcessedEvent)eventData!),
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

                _logger.LogInformation("📧 Email to User {UserId}: {Content}",
                    notification.UserId, notification.Content);
            }
        }

        // Appointment notification helpers
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

        // Billing notification helpers
        private Notification CreatePaymentInitiatedNotification(PaymentInitiatedEvent data)
        {
            var subject = "Payment Initiated";
            var content = $"Your payment of {data.Amount} for billing #{data.BillingId} has been initiated using {data.PaymentMethod}.";
            if (!string.IsNullOrEmpty(data.CheckoutUrl))
                content += $"\nPlease complete your payment at: {data.CheckoutUrl}";

            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = "user@email.com", // TODO: lấy email thực tế từ patient/user nếu cần
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Message = content,
                Type = "Email",
                RecipientEmail = "user@email.com",
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    BillingId = data.BillingId,
                    AppointmentId = data.AppointmentId,
                    Amount = data.Amount,
                    PaymentMethod = data.PaymentMethod,
                    SessionId = data.SessionId,
                    InitiatedAt = data.InitiatedAt,
                    CheckoutUrl = data.CheckoutUrl
                })
            };
        }

        private Notification CreatePaymentProcessedNotification(PaymentProcessedEvent data)
        {
            var subject = "Payment Success";
            var content = $"Your payment of {data.Amount} for billing #{data.BillingId} was processed successfully using {data.PaymentMethod}.";
            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = "user@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Message = content,
                Type = "Email",
                RecipientEmail = "user@email.com",
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    BillingId = data.BillingId,
                    AppointmentId = data.AppointmentId,
                    Amount = data.Amount,
                    PaymentMethod = data.PaymentMethod,
                    TransactionId = data.TransactionId,
                    ProcessedAt = data.ProcessedAt,
                    ProcessedByUserId = data.ProcessedByUserId,
                    ProcessedByRole = data.ProcessedByRole
                })
            };
        }

        private Notification CreatePaymentFailedNotification(PaymentFailedEvent data)
        {
            var subject = "Payment Failed";
            var content = $"Your payment of {data.Amount} for billing #{data.BillingId} failed. Reason: {data.FailureReason}";
            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = "user@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Message = content,
                Type = "Email",
                RecipientEmail = "user@email.com",
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    BillingId = data.BillingId,
                    AppointmentId = data.AppointmentId,
                    Amount = data.Amount,
                    PaymentMethod = data.PaymentMethod,
                    FailureReason = data.FailureReason,
                    FailedAt = data.FailedAt,
                    ProcessedByUserId = data.ProcessedByUserId,
                    ProcessedByRole = data.ProcessedByRole
                })
            };
        }

        private Notification CreateRefundProcessedNotification(RefundProcessedEvent data)
        {
            var subject = "Refund Processed";
            var content = $"Your refund of {data.RefundAmount} for billing #{data.BillingId} has been processed.";
            return new Notification
            {
                UserId = data.PatientId.ToString(),
                Recipient = "user@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Message = content,
                Type = "Email",
                RecipientEmail = "user@email.com",
                IsRead = false,
                Metadata = JsonConvert.SerializeObject(new
                {
                    BillingId = data.BillingId,
                    AppointmentId = data.AppointmentId,
                    OriginalAmount = data.OriginalAmount,
                    RefundAmount = data.RefundAmount,
                    PaymentMethod = data.PaymentMethod,
                    OriginalTransactionId = data.OriginalTransactionId,
                    RefundTransactionId = data.RefundTransactionId,
                    RefundedAt = data.RefundedAt,
                    RefundedByUserId = data.RefundedByUserId,
                    RefundedByRole = data.RefundedByRole
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