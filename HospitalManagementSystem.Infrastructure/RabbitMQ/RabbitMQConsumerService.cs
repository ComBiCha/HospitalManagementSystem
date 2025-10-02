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
                    .FirstOrDefault(c => c.ChannelType == NotificationChannels.Email);

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

                _logger.LogInformation("📧 Email to Patient UserId {UserId}: {Content}",
                    notification.UserId, notification.Content);
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
                    .FirstOrDefault(c => c.ChannelType == NotificationChannels.Email);

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
            
            var user = _serviceProvider.CreateScope().ServiceProvider
                .GetRequiredService<HospitalDbContext>()
                .Users.FirstOrDefault(u => u.PatientId == data.PatientId);
            
            var userId = user?.Id ?? 0;
            
            var appointmentDate = data.Date.ToString("dd/MM/yyyy HH:mm");
            var subject = "🏥 Appointment Created - Payment Required";
            var content = $@"Dear {patient?.Name ?? "Patient"},

Your appointment has been created successfully!

📋 Appointment Details:
━━━━━━━━━━━━━━━━━━━━━━━━
👨‍⚕️ Doctor: Dr. {data.DoctorName}
🏥 Specialty: {data.DoctorSpecialty}
📅 Date & Time: {appointmentDate}
🔖 Status: PENDING PAYMENT

💳 Payment Required:
━━━━━━━━━━━━━━━━━━━━━━━━
Amount: 50,000 VND (Booking Fee)
⚠️ Please complete payment within 30 minutes to confirm your appointment.

👉 Go to your portal and click ""Thanh toán ngay"" button to pay.

Thank you for choosing our hospital!

Best regards,
Hospital Management System";

            return new Notification
            {
                UserId = userId,
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                AppointmentDate = data.Date,
                Metadata = JsonConvert.SerializeObject(new
                {
                    PatientId = data.PatientId,
                    AppointmentDate = data.Date,
                    DoctorSpecialty = data.DoctorSpecialty,
                    EventType = "appointment.created",
                    BookingFee = 50000,
                    AppointmentStatus = "PendingPayment"
                })
            };
        }

        private Notification CreateAppointmentUpdatedNotification(AppointmentUpdatedEvent data, IPatientRepository patientRepository)
        {
            var patient = patientRepository.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";
            
            var user = _serviceProvider.CreateScope().ServiceProvider
                .GetRequiredService<HospitalDbContext>()
                .Users.FirstOrDefault(u => u.PatientId == data.PatientId);
            
            var userId = user?.Id ?? 0;
            
            var appointmentDate = data.Date.ToString("yyyy-MM-dd HH:mm");
            var subject = "Appointment Updated";
            var content = $"Your appointment with Dr. {data.DoctorName} has been updated. New date: {appointmentDate}.";

            return new Notification
            {
                UserId = userId,
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                AppointmentDate = data.Date,
                Metadata = JsonConvert.SerializeObject(new
                {
                    PatientId = data.PatientId,
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
            
            var user = _serviceProvider.CreateScope().ServiceProvider
                .GetRequiredService<HospitalDbContext>()
                .Users.FirstOrDefault(u => u.PatientId == data.PatientId);
            
            var userId = user?.Id ?? 0;
            
            var subject = "Appointment Cancelled";
            var content = $"Your appointment with Dr. {data.DoctorName} has been cancelled.";

            return new Notification
            {
                UserId = userId,
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                AppointmentDate = data.Date,
                Metadata = JsonConvert.SerializeObject(new
                {
                    PatientId = data.PatientId,
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
                UserId = data.PatientId,
                Recipient = "user@email.com", // TODO: lấy email thực tế từ patient/user nếu cần
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
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
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            
            var patient = patientRepo.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";
            
            var appointment = context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefault(a => a.Id == data.AppointmentId);
            
            var appointmentDetails = "";
            if (appointment != null)
            {
                appointmentDetails = $@"
📋 Appointment Details:
━━━━━━━━━━━━━━━━━━━━━━━━
👨‍⚕️ Doctor: Dr. {appointment.Doctor.Name}
🏥 Specialty: {appointment.Doctor.Specialty}
📅 Date & Time: {appointment.Date:dd/MM/yyyy HH:mm}
🔖 Status: SCHEDULED ✅
";
            }
            
            var subject = "✅ Payment Successful - Appointment Confirmed";
            var content = $@"Dear {patient?.Name ?? "Patient"},

Your payment has been processed successfully!

💳 Payment Details:
━━━━━━━━━━━━━━━━━━━━━━━━
Amount Paid: {data.Amount:N0} VND
Payment Method: {data.PaymentMethod}
Transaction ID: {data.TransactionId}
Processed At: {data.ProcessedAt:dd/MM/yyyy HH:mm}
{appointmentDetails}
Your appointment is now CONFIRMED. Please arrive 15 minutes before your scheduled time.

Thank you for choosing our hospital!

Best regards,
Hospital Management System";

            return new Notification
            {
                UserId = data.PatientId,
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                AppointmentDate = appointment?.Date,
                Metadata = JsonConvert.SerializeObject(new
                {
                    BillingId = data.BillingId,
                    AppointmentId = data.AppointmentId,
                    Amount = data.Amount,
                    PaymentMethod = data.PaymentMethod,
                    TransactionId = data.TransactionId,
                    ProcessedAt = data.ProcessedAt,
                    AppointmentStatus = "Scheduled"
                })
            };
        }

        private Notification CreatePaymentFailedNotification(PaymentFailedEvent data)
        {
            var subject = "Payment Failed";
            var content = $"Your payment of {data.Amount} for billing #{data.BillingId} failed. Reason: {data.FailureReason}";
            return new Notification
            {
                UserId = data.PatientId,
                Recipient = "user@email.com",
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
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
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            
            var patient = patientRepo.GetPatientByIdAsync(data.PatientId).Result;
            var recipientEmail = patient?.Email ?? "patient@email.com";
            
            var appointment = context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefault(a => a.Id == data.AppointmentId);
            
            var appointmentInfo = "";
            if (appointment != null)
            {
                appointmentInfo = $@"
📋 Cancelled Appointment:
━━━━━━━━━━━━━━━━━━━━━━━━
👨‍⚕️ Doctor: Dr. {appointment.Doctor.Name}
🏥 Specialty: {appointment.Doctor.Specialty}
📅 Date & Time: {appointment.Date:dd/MM/yyyy HH:mm}
🔖 Status: CANCELLED
";
            }
            
            var subject = "💰 Refund Processed - Appointment Cancelled";
            var content = $@"Dear {patient?.Name ?? "Patient"},

Your appointment has been cancelled and refund has been processed.

💸 Refund Details:
━━━━━━━━━━━━━━━━━━━━━━━━
Refund Amount: {data.RefundAmount:N0} VND
Original Payment: {data.OriginalAmount:N0} VND
Payment Method: {data.PaymentMethod}
Original Transaction ID: {data.OriginalTransactionId}
Refund Transaction ID: {data.RefundTransactionId}
Refunded At: {data.RefundedAt:dd/MM/yyyy HH:mm}
{appointmentInfo}
The refund will be credited back to your original payment method within 5-10 business days.

If you have any questions, please contact our support team.

Best regards,
Hospital Management System";

            return new Notification
            {
                UserId = data.PatientId,
                Recipient = recipientEmail,
                Subject = subject,
                Content = content,
                ChannelType = NotificationChannels.Email,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                AppointmentDate = appointment?.Date,
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