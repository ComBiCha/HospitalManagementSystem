using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Domain.Events;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPaymentRepository paymentRepository,
            IAppointmentRepository appointmentRepository,
            IPatientRepository patientRepository,
            IRabbitMQService rabbitMQService,
            IConfiguration configuration,
            ILogger<PaymentsController> logger)
        {
            _paymentRepository = paymentRepository;
            _appointmentRepository = appointmentRepository;
            _patientRepository = patientRepository;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
            _logger = logger;
            
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        }

        /// <summary>
        /// Create booking fee payment and redirect to Stripe checkout
        /// </summary>
        [HttpPost("booking-fee")]
        [Authorize(Roles = "Admin,Patient")]
        public async Task<ActionResult<CreatePaymentResponse>> CreateBookingFeePayment(CreateBookingFeeRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                // Validate appointment
                var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
                if (appointment == null)
                {
                    return NotFound($"Appointment {request.AppointmentId} not found");
                }

                // Authorization check
                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId();
                    if (userPatientId != appointment.PatientId)
                    {
                        return Forbid("You can only pay for your own appointments");
                    }
                }

                // Check if already paid
                var existingPayment = await _paymentRepository.GetByAppointmentIdAsync(request.AppointmentId);
                if (existingPayment?.Status == PaymentStatuses.Completed)
                {
                    return BadRequest("Booking fee already paid");
                }

                // Check if appointment is expired
                if (appointment.PaymentExpiresAt.HasValue && appointment.PaymentExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest("Appointment payment timeout expired");
                }

                // Get patient info
                var patient = await _patientRepository.GetPatientByIdAsync(appointment.PatientId);
                if (patient == null)
                {
                    return NotFound("Patient not found");
                }

                Payment payment;
                Session session;
                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";

                if (existingPayment?.Status == PaymentStatuses.Pending && 
                    !string.IsNullOrEmpty(existingPayment.StripeSessionId))
                {
                    try
                    {
                        var stripeSessionService = new SessionService();
                        var existingSession = await stripeSessionService.GetAsync(existingPayment.StripeSessionId);
                        
                        // Check if session is still valid (not expired)
                        if (existingSession.ExpiresAt > DateTime.UtcNow && existingSession.Status == "open")
                        {
                            payment = existingPayment;
                            _logger.LogInformation("Reusing existing payment {PaymentId} and session {SessionId}",
                                payment.Id, existingSession.Id);
                            
                            return Ok(new CreatePaymentResponse
                            {
                                PaymentId = payment.Id,
                                CheckoutUrl = existingSession.Url ?? string.Empty,
                                SessionId = existingSession.Id,
                                ExpiresAt = existingSession.ExpiresAt
                            });
                        }
                        else
                        {
                            existingPayment.Status = PaymentStatuses.Failed;
                            existingPayment.FailureReason = "Session expired, patient abandoned";
                            existingPayment.UpdatedAt = DateTime.UtcNow;
                            await _paymentRepository.UpdateAsync(existingPayment);
                            
                            _logger.LogInformation("Marked expired payment {PaymentId} as failed, creating new payment",
                                existingPayment.Id);
                        }
                    }
                    catch (StripeException)
                    {
                        existingPayment.Status = PaymentStatuses.Failed;
                        existingPayment.FailureReason = "Stripe session not found";
                        existingPayment.UpdatedAt = DateTime.UtcNow;
                        await _paymentRepository.UpdateAsync(existingPayment);
                        
                        _logger.LogWarning("Stripe session {SessionId} not found, creating new payment",
                            existingPayment.StripeSessionId);
                    }
                }
                else if (existingPayment?.Status == PaymentStatuses.Pending)
                {
                    existingPayment.Status = PaymentStatuses.Failed;
                    existingPayment.FailureReason = "No session ID, abandoned";
                    existingPayment.UpdatedAt = DateTime.UtcNow;
                    await _paymentRepository.UpdateAsync(existingPayment);
                }

                // Create new payment record
                payment = new Payment
                {
                    PatientId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    PaymentType = PaymentTypes.BookingFee,
                    Amount = appointment.BookingFee,
                    PaymentMethod = "Stripe",
                    Status = PaymentStatuses.Pending,
                    Description = $"Booking fee for appointment on {appointment.Date:yyyy-MM-dd HH:mm}",
                    CreatedAt = DateTime.UtcNow
                };

                var createdPayment = await _paymentRepository.CreateAsync(payment);

                // Create new Stripe Checkout Session
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "vnd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = "Phí đặt lịch khám",
                                    Description = payment.Description,
                                },
                                UnitAmount = (long)payment.Amount, 
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    SuccessUrl = $"{baseUrl}/api/payments/success?session_id={{CHECKOUT_SESSION_ID}}&payment_id={createdPayment.Id}",
                    CancelUrl = $"{baseUrl}/api/payments/cancel?payment_id={createdPayment.Id}",
                    CustomerEmail = patient.Email,
                    Metadata = new Dictionary<string, string>
                    {
                        { "payment_id", createdPayment.Id.ToString() },
                        { "appointment_id", appointment.Id.ToString() },
                        { "patient_id", appointment.PatientId.ToString() },
                        { "payment_type", PaymentTypes.BookingFee }
                    }
                };

                var newSessionService = new SessionService();
                session = await newSessionService.CreateAsync(options);

                // Update payment with session ID
                createdPayment.StripeSessionId = session.Id;
                await _paymentRepository.UpdateAsync(createdPayment);

                _logger.LogInformation("Created new payment {PaymentId} and Stripe session {SessionId} for appointment {AppointmentId}",
                    createdPayment.Id, session.Id, appointment.Id);

                return Ok(new CreatePaymentResponse
                {
                    PaymentId = createdPayment.Id,
                    CheckoutUrl = session.Url ?? string.Empty,
                    SessionId = session.Id,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30) 
                });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error creating payment");
                return StatusCode(500, $"Stripe error: {ex.StripeError?.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking fee payment");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Stripe payment success callback
        /// </summary>
        [HttpGet("success")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string session_id, [FromQuery] int payment_id)
        {
            try
            {
                _logger.LogInformation("Payment success callback: SessionId={SessionId}, PaymentId={PaymentId}",
                    session_id, payment_id);

                // Get Stripe session
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                // Get payment record
                var payment = await _paymentRepository.GetByIdAsync(payment_id);
                if (payment == null)
                {
                    return NotFound("Payment not found");
                }

                // Get appointment
                var appointment = await _appointmentRepository.GetByIdAsync(payment.AppointmentId ?? 0);
                if (appointment == null)
                {
                    return NotFound("Appointment not found");
                }

                // Only update if payment is still pending
                if (payment.Status == PaymentStatuses.Pending && session.PaymentStatus == "paid")
                {
                    // Update payment
                    payment.Status = PaymentStatuses.Completed;
                    payment.TransactionId = session.PaymentIntentId ?? session.Id;
                    payment.StripePaymentIntentId = session.PaymentIntentId;
                    payment.PaidAt = DateTime.UtcNow;
                    await _paymentRepository.UpdateAsync(payment);

                    // Update appointment
                    appointment.Status = "Scheduled";
                    appointment.BookingPaymentId = payment.Id;
                    appointment.PaymentExpiresAt = null; 
                    appointment.UpdatedAt = DateTime.UtcNow;
                    await _appointmentRepository.UpdateAsync(appointment);

                    _logger.LogInformation("Payment {PaymentId} completed, Appointment {AppointmentId} status updated to Scheduled",
                        payment.Id, appointment.Id);

                    // Publish payment processed event
                    await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                    {
                        BillingId = payment.Id,
                        AppointmentId = appointment.Id,
                        PatientId = payment.PatientId,
                        Amount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod,
                        TransactionId = payment.TransactionId,
                        ProcessedAt = DateTime.UtcNow,
                        ProcessedByUserId = payment.PatientId,
                        ProcessedByRole = "Patient",
                        PaymentSource = "Stripe",
                        SessionId = session.Id
                    });
                }

                
                var frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
                return Redirect($"{frontendUrl}/patient/portal?payment=success&appointment={appointment.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success");
                var frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
                return Redirect($"{frontendUrl}/patient/portal?payment=error");
            }
        }

        /// <summary>
        /// Stripe payment cancel callback
        /// </summary>
        [HttpGet("cancel")]
        [AllowAnonymous]
        public IActionResult PaymentCancel([FromQuery] int payment_id)
        {
            _logger.LogInformation("Payment cancelled: PaymentId={PaymentId}", payment_id);
            
            var frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
            return Redirect($"{frontendUrl}/patient/portal?payment=cancelled");
        }

        /// <summary>
        /// Stripe webhook for payment events
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> StripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _configuration["Stripe:WebhookSecret"]
                );

                _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case Events.CheckoutSessionCompleted:
                        var session = stripeEvent.Data.Object as Session;
                        await HandleCheckoutSessionCompleted(session);
                        break;

                    case Events.PaymentIntentSucceeded:
                        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentSucceeded(paymentIntent);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook");
                return BadRequest();
            }
        }

        private async Task HandleCheckoutSessionCompleted(Session? session)
        {
            if (session?.Metadata?.ContainsKey("payment_id") != true) return;

            var paymentId = int.Parse(session.Metadata["payment_id"]);
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            
            if (payment != null && payment.Status == PaymentStatuses.Pending)
            {
                payment.Status = PaymentStatuses.Completed;
                payment.TransactionId = session.PaymentIntentId ?? session.Id;
                payment.StripePaymentIntentId = session.PaymentIntentId;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                // Update appointment
                if (payment.AppointmentId.HasValue)
                {
                    var appointment = await _appointmentRepository.GetByIdAsync(payment.AppointmentId.Value);
                    if (appointment != null)
                    {
                        appointment.Status = "Scheduled";
                        appointment.BookingPaymentId = payment.Id;
                        appointment.PaymentExpiresAt = null;
                        await _appointmentRepository.UpdateAsync(appointment);
                    }
                }

                _logger.LogInformation("Webhook: Payment {PaymentId} completed", paymentId);
            }
        }

        private async Task HandlePaymentIntentSucceeded(PaymentIntent? paymentIntent)
        {
            // Similar to HandleCheckoutSessionCompleted
            await Task.CompletedTask;
        }

        // Helper methods
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private int? GetCurrentUserPatientId()
        {
            var patientIdClaim = User.FindFirst("PatientId")?.Value;
            return int.TryParse(patientIdClaim, out int patientId) ? patientId : null;
        }

        /// <summary>
        /// Refund booking fee payment
        /// </summary>
        [HttpPost("{paymentId}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<RefundPaymentResponse>> RefundBookingFee(int paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return NotFound($"Payment {paymentId} not found");
                }

                if (payment.Status != PaymentStatuses.Completed)
                {
                    return BadRequest($"Cannot refund payment with status: {payment.Status}");
                }

                if (payment.PaymentType != PaymentTypes.BookingFee)
                {
                    return BadRequest("Only booking fee payments can be refunded");
                }

                if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                {
                    return BadRequest("No Stripe payment intent found");
                }

                _logger.LogInformation("Processing refund for payment {PaymentId}, amount: {Amount}",
                    payment.Id, payment.Amount);

                // Create Stripe refund
                var refundService = new RefundService();
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = payment.StripePaymentIntentId,
                    Amount = (long)payment.Amount, 
                    Reason = RefundReasons.RequestedByCustomer,
                    Metadata = new Dictionary<string, string>
                    {
                        { "payment_id", payment.Id.ToString() },
                        { "appointment_id", payment.AppointmentId?.ToString() ?? "" }
                    }
                };

                var refund = await refundService.CreateAsync(refundOptions);

                // Update payment status
                payment.Status = PaymentStatuses.Refunded;
                payment.FailureReason = "Refunded by admin";
                payment.UpdatedAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                _logger.LogInformation("Refund successful: PaymentId={PaymentId}, RefundId={RefundId}",
                    payment.Id, refund.Id);

                return Ok(new RefundPaymentResponse
                {
                    PaymentId = payment.Id,
                    RefundId = refund.Id,
                    Amount = payment.Amount,
                    Status = refund.Status,
                    RefundedAt = DateTime.UtcNow
                });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error refunding payment {PaymentId}", paymentId);
                return StatusCode(500, $"Stripe error: {ex.StripeError?.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding payment {PaymentId}", paymentId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // DTOs
    public class CreateBookingFeeRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int AppointmentId { get; set; }
    }

    public class CreatePaymentResponse
    {
        public int PaymentId { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class RefundPaymentResponse
    {
        public int PaymentId { get; set; }
        public string RefundId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RefundedAt { get; set; }
    }
}