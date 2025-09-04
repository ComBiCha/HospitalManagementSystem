using Stripe;
using Stripe.Checkout;
using System.Text.Json;

namespace BillingService.API.Services.PaymentMethods
{
    public class StripePaymentMethod : IPaymentMethod
    {
        public string PaymentMethodName => "Stripe";
        
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripePaymentMethod> _logger;

        public StripePaymentMethod(IConfiguration configuration, ILogger<StripePaymentMethod> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // Set Stripe API key
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing Stripe payment for amount: {Amount} {Currency}", 
                    request.Amount, request.Currency);

                return await CreateCheckoutSessionAsync(request);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error processing payment: {Error}", ex.StripeError?.Message);
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = $"Stripe error: {ex.StripeError?.Message ?? ex.Message}",
                    Amount = request.Amount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Stripe payment");
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = $"Payment processing error: {ex.Message}",
                    Amount = request.Amount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        private async Task<PaymentResult> CreateCheckoutSessionAsync(PaymentRequest request)
        {
            var customerEmail = GetStringFromAdditionalData(request.AdditionalData, "customer_email");
            var metadataObj = GetObjectFromAdditionalData(request.AdditionalData, "metadata");
            
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:7001";
            var billingId = request.AdditionalData.GetValueOrDefault("billing_id", request.AppointmentId);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = request.Currency.ToLower(),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Hospital Payment",
                                Description = request.Description ?? $"Medical services for appointment #{request.AppointmentId}",
                            },
                            UnitAmount = (long)(request.Amount * 100), // Convert to cents
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = $"{baseUrl}/payment/success?session_id={{CHECKOUT_SESSION_ID}}&billing_id={billingId}",
                CancelUrl = $"{baseUrl}/payment/cancel?billing_id={billingId}",
                Metadata = new Dictionary<string, string>
                {
                    { "billing_id", billingId.ToString() ?? "" },
                    { "patient_id", request.PatientId.ToString() },
                    { "appointment_id", request.AppointmentId.ToString() },
                    { "amount", request.Amount.ToString("F2") },
                    { "currency", request.Currency }
                }
            };

            // Add customer email if provided
            if (!string.IsNullOrEmpty(customerEmail))
            {
                options.CustomerEmail = customerEmail;
            }

            // Add additional metadata from request
            if (metadataObj is Dictionary<string, object> metadata)
            {
                foreach (var kvp in metadata)
                {
                    if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()))
                    {
                        options.Metadata[kvp.Key] = kvp.Value.ToString() ?? "";
                    }
                }
            }

            _logger.LogInformation("Creating Stripe Checkout Session for amount: {Amount} {Currency}", 
                request.Amount, request.Currency);

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("Stripe Checkout Session created successfully: {SessionId}", session.Id);

            return new PaymentResult
            {
                IsSuccess = true,
                TransactionId = session.Id,
                Amount = request.Amount,
                ProcessedAt = DateTime.UtcNow,
                AdditionalData = new Dictionary<string, object>
                {
                    { "checkout_url", session.Url ?? "" },
                    { "session_id", session.Id },
                    { "payment_status", session.PaymentStatus ?? "" },
                    { "customer_email", customerEmail ?? "" },
                    { "expires_at", session.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "" }
                }
            };
        }

        public async Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal amount)
        {
            try
            {
                _logger.LogInformation("Processing Stripe refund for session: {SessionId}, amount: {Amount}", 
                    transactionId, amount);

                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(transactionId);

                if (string.IsNullOrEmpty(session.PaymentIntentId))
                {
                    throw new InvalidOperationException("No payment intent found for this session");
                }

                var refundService = new RefundService();
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = session.PaymentIntentId,
                    Amount = (long)(amount * 100), // Convert to cents
                    Metadata = new Dictionary<string, string>
                    {
                        { "original_session_id", transactionId },
                        { "refund_reason", "Customer request" }
                    }
                };

                var refund = await refundService.CreateAsync(refundOptions);

                _logger.LogInformation("Stripe refund processed successfully: {RefundId}", refund.Id);

                return new PaymentResult
                {
                    IsSuccess = true,
                    TransactionId = refund.Id,
                    Amount = amount,
                    ProcessedAt = DateTime.UtcNow,
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "refund_id", refund.Id },
                        { "original_session_id", transactionId },
                        { "payment_intent_id", session.PaymentIntentId },
                        { "refund_status", refund.Status }
                    }
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error processing refund: {Error}", ex.StripeError?.Message);
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = $"Stripe refund error: {ex.StripeError?.Message ?? ex.Message}",
                    Amount = amount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Stripe refund");
                return new PaymentResult
                {
                    IsSuccess = false,
                    FailureReason = $"Refund processing error: {ex.Message}",
                    Amount = amount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        private string? GetStringFromAdditionalData(Dictionary<string, object>? additionalData, string key)
        {
            if (additionalData?.ContainsKey(key) != true) 
                return null;
            
            var value = additionalData[key];
            return value switch
            {
                string stringValue => stringValue,
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                _ => value?.ToString()
            };
        }

        private object? GetObjectFromAdditionalData(Dictionary<string, object>? additionalData, string key)
        {
            if (additionalData?.ContainsKey(key) != true) 
                return null;
            
            return additionalData[key];
        }
    }
}