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
using HospitalManagementSystem.Domain.Caching;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Domain.Payments;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IMedicalRecordRepository _medicalRecordRepository; 
        private readonly IPatientRepository _patientRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IStripePaymentService _stripePaymentService;

        public PaymentsController(
            IPaymentRepository paymentRepository,
            IAppointmentRepository appointmentRepository,
            IMedicalRecordRepository medicalRecordRepository, 
            IPatientRepository patientRepository,
            IRabbitMQService rabbitMQService,
            IConfiguration configuration,
            ICacheService cacheService,
            ILogger<PaymentsController> logger,
            IStripePaymentService stripePaymentService)
        {
            _paymentRepository = paymentRepository;
            _appointmentRepository = appointmentRepository;
            _medicalRecordRepository = medicalRecordRepository; 
            _patientRepository = patientRepository;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
            _cacheService = cacheService;
            _logger = logger;
            _stripePaymentService = stripePaymentService;
        }

        [HttpGet("{paymentId}/status")]
        [Authorize(Roles = "Admin,Accountant")]
        public async Task<ActionResult<string>> GetPaymentStatus(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
            {
                return NotFound("Payment not found");
            }
            return Ok(payment.Status);
        }

        [HttpPost("online/create-intent")]
        [Authorize(Roles = "Patient")]
        public async Task<ActionResult> CreateOnlineAppointmentPaymentIntent([FromBody] CreateBookingFeeRequest request)
        {
            try
            {
                var patientId = GetCurrentUserPatientId();
                if (!patientId.HasValue)
                {
                    return Unauthorized("Patient ID not found in token.");
                }

                var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
                if (appointment == null)
                {
                    return NotFound(new { message = "Appointment not found." });
                }

                if (appointment.PatientId != patientId.Value)
                {
                    return Forbid("You can only pay for your own appointments.");
                }

                if (appointment.Type != AppointmentType.Online)
                {
                    return BadRequest(new { message = "This endpoint is only for online appointments." });
                }
                
                if (appointment.Status != "PendingPayment")
                {
                    return BadRequest(new { message = $"Appointment status is '{appointment.Status}', not 'PendingPayment'." });
                }

                if (appointment.PaymentExpiresAt.HasValue && appointment.PaymentExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest(new { message = "The payment window for this appointment has expired." });
                }

                Payment paymentToProcess = null;
                // Attempt to find the most recent pending payment for this appointment
                var pendingPayment = await _paymentRepository.GetLatestPendingBookingFeePaymentForAppointmentAsync(request.AppointmentId);

                if (pendingPayment != null)
                {
                    // Case 1: Existing pending payment has a StripePaymentIntentId
                    if (!string.IsNullOrEmpty(pendingPayment.StripePaymentIntentId))
                    {
                        var intent = await _stripePaymentService.GetPaymentIntentAsync(pendingPayment.StripePaymentIntentId);
                        if (intent != null && (intent.Status == "requires_payment_method" || intent.Status == "requires_action" || intent.Status == "processing"))
                        {
                            _logger.LogInformation("Reusing existing PaymentIntent {PaymentIntentId} for Appointment {AppointmentId}. Payment {PaymentId}.", intent.Id, request.AppointmentId, pendingPayment.Id);
                            paymentToProcess = pendingPayment; // Reuse the existing payment record
                            return Ok(new { clientSecret = intent.ClientSecret });
                        }
                        else
                        {
                            // Intent is not reusable or has a final status (e.g., succeeded, canceled), mark payment as failed
                            pendingPayment.Status = PaymentStatuses.Failed;
                            pendingPayment.FailureReason = $"Stripe PaymentIntent status not reusable: {intent?.Status ?? "unknown"}";
                            pendingPayment.UpdatedAt = DateTime.UtcNow;
                            await _paymentRepository.UpdateAsync(pendingPayment);
                            _logger.LogInformation("Existing PaymentIntent {PaymentIntentId} for Appointment {AppointmentId} found not reusable (status: {Status}). Marking payment {PaymentId} as failed.", intent?.Id, request.AppointmentId, pendingPayment.Id, intent?.Status);
                            // Proceed to create a new payment
                        }
                    }
                    else
                    {
                        // Case 2: Existing pending payment without StripePaymentIntentId (just created by CreateOnlineAppointmentAsync)
                        // This is a fresh pending payment, proceed to create a Stripe PaymentIntent for it.
                        _logger.LogInformation("Existing pending Payment {PaymentId} for Appointment {AppointmentId} found without StripePaymentIntentId. Creating one.", pendingPayment.Id, request.AppointmentId);
                        paymentToProcess = pendingPayment;
                    }
                }
                else
                {
                    // No pending payment found, create a new one.
                    _logger.LogInformation("No pending Payment found for Appointment {AppointmentId}. Creating a new one.", request.AppointmentId);
                    paymentToProcess = new Payment
                    {
                        PatientId = appointment.PatientId,
                        AppointmentId = appointment.Id,
                        PaymentType = PaymentTypes.BookingFee,
                        Amount = appointment.BookingFee,
                        PaymentMethod = "Stripe",
                        Status = PaymentStatuses.Pending,
                        Description = $"Booking fee for online appointment on {appointment.Date:yyyy-MM-dd HH:mm}",
                        CreatedAt = DateTime.UtcNow
                    };
                    paymentToProcess = await _paymentRepository.CreateAsync(paymentToProcess);
                    _logger.LogInformation("Created new Payment record {PaymentId} for Appointment {AppointmentId}", paymentToProcess.Id, request.AppointmentId);
                }

                // At this point, paymentToProcess is either a reused pending payment or a newly created one.
                // If it doesn't have a StripePaymentIntentId (either it's new, or an old pending one without an ID),
                // create a new Stripe PaymentIntent for it.
                if (paymentToProcess == null)
                {
                    // Should not happen – throw or handle error appropriately
                    throw new InvalidOperationException("paymentToProcess is not assigned.");
                }
                if (string.IsNullOrEmpty(paymentToProcess.StripePaymentIntentId))
                {
                    var paymentIntent = await _stripePaymentService.CreatePaymentIntentAsync(
                        (long)paymentToProcess.Amount,
                        "vnd",
                        $"Booking fee for online appointment #{appointment.Id}",
                        new Dictionary<string, string>
                        {
                            { "payment_id", paymentToProcess.Id.ToString() },
                            { "appointment_id", appointment.Id.ToString() },
                            { "patient_id", appointment.PatientId.ToString() }
                        });

                    paymentToProcess.StripePaymentIntentId = paymentIntent.Id;
                    await _paymentRepository.UpdateAsync(paymentToProcess);
                    _logger.LogInformation("Created Stripe PaymentIntent {PaymentIntentId} and linked to Payment {PaymentId}", paymentIntent.Id, paymentToProcess.Id);
                    return Ok(new { clientSecret = paymentIntent.ClientSecret });
                }

                // If we reach here, it means we found and reused a payment intent from an existing record (e.g. status requires_action)
                // and its clientSecret was returned earlier. This branch should ideally not be reached if previous logic is correct
                // but is a safeguard.
                _logger.LogWarning("Reached unexpected branch in CreateOnlineAppointmentPaymentIntent for Appointment {AppointmentId}, Payment {PaymentId}. Returning clientSecret from existing intent.", request.AppointmentId, paymentToProcess.Id);
                var finalIntent = await _stripePaymentService.GetPaymentIntentAsync(paymentToProcess.StripePaymentIntentId);
                return Ok(new { clientSecret = finalIntent.ClientSecret });

            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error creating payment intent for appointment {AppointmentId}", request.AppointmentId);
                return StatusCode(500, new { message = $"Stripe error: {ex.StripeError?.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment intent for appointment {AppointmentId}", request.AppointmentId);
                return StatusCode(500, new { message = "An internal error occurred." });
            }
        }

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
                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";

                if (existingPayment?.Status == PaymentStatuses.Pending && 
                    !string.IsNullOrEmpty(existingPayment.StripeSessionId))
                {
                    try
                    {
                        var stripeSessionService = new SessionService();
                        var existingSession = await stripeSessionService.GetAsync(existingPayment.StripeSessionId);
                        
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

                var metadata = new Dictionary<string, string>
                {
                    { "payment_id", createdPayment.Id.ToString() },
                    { "appointment_id", appointment.Id.ToString() },
                    { "patient_id", appointment.PatientId.ToString() },
                    { "payment_type", PaymentTypes.BookingFee }
                };

                var session = await _stripePaymentService.CreateCheckoutSessionAsync(
                    (long)createdPayment.Amount,
                    "Phí đặt lịch khám",
                    payment.Description,
                    patient.Email,
                    $"{baseUrl}/api/payments/success?payment_id={createdPayment.Id}",
                    $"{baseUrl}/api/payments/cancel?payment_id={createdPayment.Id}",
                    metadata
                );

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
                    ExpiresAt = session.ExpiresAt
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

        [HttpGet("success")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess([FromQuery] int payment_id)
        {
            _logger.LogInformation("Redirecting from successful payment: PaymentId={PaymentId}", payment_id);
            var payment = await _paymentRepository.GetByIdAsync(payment_id);
            var appointmentId = payment?.AppointmentId ?? 0;

            var frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
            return Redirect($"{frontendUrl}/patient/portal?payment=success&appointment={appointmentId}");
        }

        [HttpGet("cancel")]
        [AllowAnonymous]
        public IActionResult PaymentCancel([FromQuery] int payment_id)
        {
            _logger.LogInformation("Redirecting from cancelled payment: PaymentId={PaymentId}", payment_id);
            var frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
            return Redirect($"{frontendUrl}/patient/portal?payment=cancelled");
        }

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
                    _configuration["Stripe:WebhookSecret"],
                    throwOnApiVersionMismatch: false
                );

                _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case Events.CheckoutSessionCompleted:
                        var session = stripeEvent.Data.Object as Session;
                        await HandleCheckoutSessionCompleted(session);
                        break;

                    case Events.CheckoutSessionExpired:
                        var expiredSession = stripeEvent.Data.Object as Session;
                        await HandleCheckoutSessionExpired(expiredSession);
                        break;

                    case Events.PaymentIntentSucceeded:
                        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentSucceeded(paymentIntent);
                        break;

                    case Events.PaymentIntentPaymentFailed:
                        var failedPaymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentIntentPaymentFailed(failedPaymentIntent);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        private async Task HandleCheckoutSessionCompleted(Session? session)
        {
            if (session?.Metadata?.ContainsKey("payment_id") != true) return;

            var paymentId = int.Parse(session.Metadata["payment_id"]);
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            
            if (payment != null && payment.Status == PaymentStatuses.Pending)
            {
                // Universal update for the payment itself
                payment.Status = PaymentStatuses.Completed;
                payment.TransactionId = session.PaymentIntentId ?? session.Id;
                payment.StripePaymentIntentId = session.PaymentIntentId;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                var paymentType = session.Metadata.ContainsKey("payment_type") ? session.Metadata["payment_type"] : null;
                int? medicalRecordId = payment.MedicalRecordId;

                // If it's a deposit, try to find the medical record via the appointment
                if (!medicalRecordId.HasValue && (paymentType == PaymentTypes.Deposit || paymentType == PaymentTypes.BookingFee) && payment.AppointmentId.HasValue)
                {
                    var medicalRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(payment.AppointmentId.Value);
                    if (medicalRecord != null)
                    {
                        medicalRecordId = medicalRecord.Id;
                    }
                }

                // Handle Appointment status update for BookingFee
                if (paymentType == PaymentTypes.BookingFee && payment.AppointmentId.HasValue)
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

                // Unified logic to update Medical Record if one is found/linked
                if (medicalRecordId.HasValue)
                {
                    var medicalRecord = await _medicalRecordRepository.GetByIdAsync(medicalRecordId.Value);
                    if (medicalRecord != null)
                    {
                        var completedDeposits = await _paymentRepository.GetCompletedDepositsByAppointmentIdAsync(medicalRecord.AppointmentId);
                        var completedMedicalPayments = await _paymentRepository.GetCompletedPaymentsByMedicalRecordIdAsync(medicalRecord.Id);
                        var allPayments = completedDeposits.Union(completedMedicalPayments).DistinctBy(p => p.Id);
                        medicalRecord.PaidAmount = allPayments.Sum(p => p.Amount);

                        if (paymentType == PaymentTypes.FinalPayment && medicalRecord.PaidAmount >= medicalRecord.TotalFee)
                        {
                            medicalRecord.PaymentStatus = "Paid";
                        }
                        else
                        {
                            medicalRecord.PaymentStatus = "Unpaid";
                        }
                        await _medicalRecordRepository.UpdateAsync(medicalRecord);

                        // Invalidate cache for the patient's medical records
                        await _cacheService.RemovePatternAsync($"patient:{medicalRecord.PatientId}:medical-records:*");
                        _logger.LogInformation("Cleared medical record cache for patient {PatientId} due to webhook payment completion.", medicalRecord.PatientId);
                    }
                }

                await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                {
                    BillingId = payment.Id,
                    AppointmentId = payment.AppointmentId ?? 0,
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

                _logger.LogInformation("Webhook: Payment {PaymentId} completed", paymentId);
            }
        }

        private async Task HandleCheckoutSessionExpired(Session? session)
        {
            if (session?.Metadata?.ContainsKey("payment_id") != true) return;

            var paymentId = int.Parse(session.Metadata["payment_id"]);
            var payment = await _paymentRepository.GetByIdAsync(paymentId);

            if (payment != null && payment.Status == PaymentStatuses.Pending)
            {
                payment.Status = PaymentStatuses.Failed;
                payment.FailureReason = "Stripe checkout session expired";
                await _paymentRepository.UpdateAsync(payment);

                _logger.LogWarning("Webhook: Payment {PaymentId} failed due to expired session", paymentId);
            }
        }

        private async Task HandlePaymentIntentSucceeded(PaymentIntent? paymentIntent)
        {
            if (paymentIntent?.Metadata?.ContainsKey("payment_id") != true) return;

            var paymentId = int.Parse(paymentIntent.Metadata["payment_id"]);
            var payment = await _paymentRepository.GetByIdAsync(paymentId);

            if (payment != null && payment.Status == PaymentStatuses.Pending)
            {
                payment.Status = PaymentStatuses.Completed;
                payment.TransactionId = paymentIntent.Id;
                payment.StripePaymentIntentId = paymentIntent.Id;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

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

                await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                {
                    BillingId = payment.Id,
                    AppointmentId = payment.AppointmentId ?? 0,
                    PatientId = payment.PatientId,
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod,
                    TransactionId = payment.TransactionId,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessedByUserId = payment.PatientId,
                    ProcessedByRole = "Patient",
                    PaymentSource = "Stripe_PaymentIntent",
                    SessionId = paymentIntent.Id
                });

                _logger.LogInformation("Webhook: PaymentIntent {PaymentIntentId} succeeded for Payment {PaymentId}", paymentIntent.Id, paymentId);
            }
        }

        private async Task HandlePaymentIntentPaymentFailed(PaymentIntent? paymentIntent)
        {
            if (paymentIntent?.Metadata?.ContainsKey("payment_id") != true) return;

            var paymentId = int.Parse(paymentIntent.Metadata["payment_id"]);
            var payment = await _paymentRepository.GetByIdAsync(paymentId);

            if (payment != null && payment.Status == PaymentStatuses.Pending)
            {
                payment.Status = PaymentStatuses.Failed;
                payment.FailureReason = paymentIntent.LastPaymentError?.Message ?? "Payment failed in Stripe.";
                await _paymentRepository.UpdateAsync(payment);

                _logger.LogWarning("Webhook: PaymentIntent {PaymentIntentId} failed for Payment {PaymentId}. Reason: {Reason}", paymentIntent.Id, paymentId, payment.FailureReason);
            }
        }

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