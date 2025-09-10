using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Domain.Events;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("payment")]
    public class PaymentController : ControllerBase
    {
        private readonly IBillingRepository _billingRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IBillingRepository billingRepository,
            IRabbitMQService rabbitMQService,
            ILogger<PaymentController> logger,
            IConfiguration configuration)
        {
            _billingRepository = billingRepository;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
            
            // Ensure Stripe is configured
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        }

        [HttpGet("success")]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string session_id, [FromQuery] int billing_id)
        {
            try
            {
                if (string.IsNullOrEmpty(session_id))
                    return BadRequest("Session ID is required");

                _logger.LogInformation("Payment success callback for session: {SessionId}, billing: {BillingId}", session_id, billing_id);

                // Get session details from Stripe
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                // Find billing record
                var billing = await _billingRepository.GetByIdAsync(billing_id);
                if (billing == null)
                    return NotFound($"Billing not found for id {billing_id}");

                // Update billing status if paid
                if (session.PaymentStatus == "paid")
                {
                    billing.Status = "Completed";
                    billing.TransactionId = session.PaymentIntentId ?? session.Id;
                    billing.UpdatedAt = DateTime.UtcNow;
                    await _billingRepository.UpdateAsync(billing);

                    // Optionally publish event here
                    await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                    {
                        BillingId = billing.Id,
                        AppointmentId = billing.AppointmentId,
                        PatientId = billing.PatientId,
                        Amount = billing.Amount,
                        PaymentMethod = billing.PaymentMethod,
                        TransactionId = billing.TransactionId,
                        ProcessedAt = DateTime.UtcNow,
                        PaymentSource = "Stripe Checkout",
                        SessionId = session_id
                    });
                }

                // Return success page
                return Content($@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Payment Successful - HMS</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 50px; text-align: center; }}
                        .success {{ color: #28a745; }}
                        .container {{ max-width: 600px; margin: 0 auto; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1 class='success'>✅ Payment Successful!</h1>
                        <h2>Hospital Management System</h2>
                        <p><strong>Billing ID:</strong> {billing_id}</p>
                        <p><strong>Amount:</strong> ${billing.Amount:F2}</p>
                        <p><strong>Transaction ID:</strong> {billing.TransactionId}</p>
                        <p><strong>Status:</strong> {billing.Status}</p>
                        <hr>
                        <p>Thank you for your payment. Your billing has been processed successfully.</p>
                        <button onclick='window.close()'>Close Window</button>
                    </div>
                </body>
                </html>", "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success callback");
                return StatusCode(500, "Error processing payment confirmation");
            }
        }

        [HttpGet("cancel")]
        public IActionResult PaymentCancel([FromQuery] int appointment_id)
        {
            _logger.LogInformation("Payment cancelled for appointment: {AppointmentId}", appointment_id);

            return Content($@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Payment Cancelled - HMS</title>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 50px; text-align: center; }}
                    .cancel {{ color: #dc3545; }}
                    .container {{ max-width: 600px; margin: 0 auto; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h1 class='cancel'>❌ Payment Cancelled</h1>
                    <h2>Hospital Management System</h2>
                    <p><strong>Appointment ID:</strong> {appointment_id}</p>
                    <hr>
                    <p>Your payment was cancelled. You can try again or contact our support team.</p>
                    <button onclick='window.close()'>Close Window</button>
                </div>
            </body>
            </html>", "text/html");
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "whsec_test"
                );

                _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

                // Handle different event types
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
            if (session?.Metadata?.ContainsKey("appointment_id") == true)
            {
                var appointmentId = int.Parse(session.Metadata["appointment_id"]);
                var billing = await _billingRepository.GetByAppointmentIdAsync(appointmentId);
                
                if (billing != null)
                {
                    billing.Status = "Completed";
                    billing.TransactionId = session.PaymentIntentId ?? session.Id;
                    billing.UpdatedAt = DateTime.UtcNow;
                    await _billingRepository.UpdateAsync(billing);

                    // Publish event
                    await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                    {
                        BillingId = billing.Id,
                        AppointmentId = billing.AppointmentId,
                        PatientId = billing.PatientId,
                        Amount = billing.Amount,
                        PaymentMethod = billing.PaymentMethod,
                        TransactionId = billing.TransactionId,
                        ProcessedAt = DateTime.UtcNow,
                        PaymentSource = "Stripe Webhook",
                        SessionId = session.Id
                    });

                    _logger.LogInformation("Billing updated & event published via webhook for appointment: {AppointmentId}", appointmentId);
                }
            }
        }

        private async Task HandlePaymentIntentSucceeded(PaymentIntent? paymentIntent)
        {
            if (paymentIntent?.Metadata?.ContainsKey("appointment_id") == true)
            {
                var appointmentId = int.Parse(paymentIntent.Metadata["appointment_id"]);
                var billing = await _billingRepository.GetByAppointmentIdAsync(appointmentId);
                
                if (billing != null && billing.Status == "Pending")
                {
                    billing.Status = "Completed";
                    billing.TransactionId = paymentIntent.Id;
                    billing.UpdatedAt = DateTime.UtcNow;
                    await _billingRepository.UpdateAsync(billing);

                    // Publish event
                    await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                    {
                        BillingId = billing.Id,
                        AppointmentId = billing.AppointmentId,
                        PatientId = billing.PatientId,
                        Amount = billing.Amount,
                        PaymentMethod = billing.PaymentMethod,
                        TransactionId = billing.TransactionId,
                        ProcessedAt = DateTime.UtcNow,
                        PaymentSource = "Stripe Webhook",
                        SessionId = paymentIntent.Id
                    });

                    _logger.LogInformation("Billing updated & event published via PaymentIntent webhook for appointment: {AppointmentId}", appointmentId);
                }
            }
        }
    }
}
