using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Payments;
using HospitalManagementSystem.Domain.Factories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Infrastructure.RabbitMQ;
using HospitalManagementSystem.Domain.Events;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Domain.Strategies;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BillingsController : ControllerBase
    {
        private readonly IBillingStrategyFactory _billingStrategyFactory;
        private readonly IBillingRepository _billingRepository;
        private readonly IPaymentFactory _paymentFactory;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<BillingsController> _logger;

        public BillingsController(
            IBillingStrategyFactory billingStrategyFactory,
            IBillingRepository billingRepository,
            IPaymentFactory paymentFactory,
            IRabbitMQService rabbitMQService,
            ILogger<BillingsController> logger)
        {
            _billingStrategyFactory = billingStrategyFactory;
            _billingRepository = billingRepository;
            _paymentFactory = paymentFactory;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<ActionResult<IEnumerable<BillingDto>>> GetAllBillings()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                _logger.LogInformation("User {UserId} with role {Role} getting all billings",
                    currentUserId, currentUserRole);

                var billings = await _billingRepository.GetAllAsync();
                var billingDtos = billings.Select(MapToBillingDto);

                return Ok(billingDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all billings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BillingDto>> GetBilling(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                var billing = await _billingRepository.GetByIdAsync(id);
                if (billing == null)
                {
                    return NotFound($"Billing with ID {id} not found");
                }

                if (!CanAccessBilling(billing, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to access this billing");
                }

                var billingDto = MapToBillingDto(billing);
                return Ok(billingDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting billing with ID: {BillingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("appointment/{appointmentId}")]
        public async Task<ActionResult<BillingDto>> GetBillingByAppointment(int appointmentId)
        {
            try
            {
                var billing = await _billingRepository.GetByAppointmentIdAsync(appointmentId);
                if (billing == null)
                {
                    return NotFound($"No billing found for appointment {appointmentId}");
                }

                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (!CanAccessBilling(billing, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to access this billing");
                }

                var billingDto = MapToBillingDto(billing);
                return Ok(billingDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting billing for appointment: {AppointmentId}", appointmentId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("patient/{patientId}")]
        public async Task<ActionResult<IEnumerable<BillingDto>>> GetBillingsByPatient(int patientId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId();
                    if (userPatientId != patientId)
                    {
                        return Forbid("You can only view your own billing records");
                    }
                }

                var billings = await _billingRepository.GetByPatientIdAsync(patientId);
                var billingDtos = billings.Select(MapToBillingDto);

                return Ok(billingDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting billings for patient: {PatientId}", patientId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        public async Task<ActionResult<BillingDto>> CreateBilling(CreateBillingRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId();
                    if (userPatientId != request.PatientId)
                    {
                        return Forbid("You can only create billing for yourself");
                    }
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if billing already exists for this appointment
                var existingBilling = await _billingRepository.GetByAppointmentIdAsync(request.AppointmentId);
                if (existingBilling != null)
                {
                    return Conflict($"Billing already exists for appointment {request.AppointmentId}");
                }

                var billingStrategy = _billingStrategyFactory.CreateBillingStrategy(request.BillingType);

                // Create billing record
                var billing = new Billing
                {
                    AppointmentId = request.AppointmentId,
                    PatientId = request.PatientId,
                    Amount = request.Amount,
                    BillingType = request.BillingType,
                    InsuranceNumber = request.InsuranceNumber,
                    CompanyId = request.CompanyId,
                    PaymentMethod = request.PaymentMethod,
                    Status = "Pending",
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await billingStrategy.ValidateBillingAsync(billing);

                // Calculate total amount using strategy
                billing.TotalAmount = await billingStrategy.CalculateTotalAmountAsync(billing);

                // Generate invoice number using strategy
                billing.InvoiceNumber = await billingStrategy.GenerateInvoiceNumberAsync(billing);

                // Save billing
                await _billingRepository.CreateAsync(billing);

                return Ok(billing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating billing");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/process-payment")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        public async Task<ActionResult<PaymentResultDto>> ProcessPayment(int id, ProcessPaymentRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                var billing = await _billingRepository.GetByIdAsync(id);
                if (billing == null)
                {
                    return NotFound($"Billing with ID {id} not found");
                }

                if (!CanAccessBilling(billing, currentUserId, currentUserRole))
                {
                    return Forbid("You don't have permission to process this payment");
                }

                if (billing.Status != "Pending")
                {
                    return BadRequest($"Cannot process payment for billing with status: {billing.Status}");
                }

                _logger.LogInformation("Processing payment for billing {BillingId} using {PaymentMethod}",
                    id, billing.PaymentMethod);

                // Use factory to create payment method
                var paymentMethod = _paymentFactory.CreatePaymentMethod(billing.PaymentMethod);

                var paymentRequest = new PaymentRequest
                {
                    Amount = billing.TotalAmount, // Use strategy-calculated amount
                    Currency = request.Currency ?? "USD",
                    Description = billing.Description ?? $"Payment for appointment {billing.AppointmentId}",
                    PatientId = billing.PatientId,
                    AppointmentId = billing.AppointmentId,
                    AdditionalData = request.AdditionalData ?? new Dictionary<string, object>()
                };

                // Add billing context to additional data
                paymentRequest.AdditionalData["billing_id"] = billing.Id;
                paymentRequest.AdditionalData["billing_type"] = billing.BillingType;
                paymentRequest.AdditionalData["invoice_number"] = billing.InvoiceNumber ?? "";

                var paymentResult = await paymentMethod.ProcessPaymentAsync(paymentRequest);

                // For Stripe, don't mark as completed immediately (will be done via webhook)
                // For other methods like Cash, mark as completed
                if (billing.PaymentMethod.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
                {
                    billing.Status = paymentResult.IsSuccess ? "Processing" : "Failed";
                }
                else
                {
                    billing.Status = paymentResult.IsSuccess ? "Completed" : "Failed";
                }

                billing.TransactionId = paymentResult.TransactionId;
                billing.FailureReason = paymentResult.FailureReason;
                billing.UpdatedAt = DateTime.UtcNow;

                await _billingRepository.UpdateAsync(billing);

                // Publish events to RabbitMQ
                if (paymentResult.IsSuccess)
                {
                    if (billing.PaymentMethod.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
                    {
                        await _rabbitMQService.PublishPaymentInitiatedAsync(new PaymentInitiatedEvent
                        {
                            BillingId = billing.Id,
                            AppointmentId = billing.AppointmentId,
                            PatientId = billing.PatientId,
                            Amount = billing.TotalAmount,
                            PaymentMethod = billing.PaymentMethod,
                            SessionId = billing.TransactionId,
                            InitiatedAt = paymentResult.ProcessedAt,
                            CheckoutUrl = paymentResult.AdditionalData.GetValueOrDefault("checkout_url") as string
                        });
                    }
                    else
                    {
                        await _rabbitMQService.PublishPaymentProcessedAsync(new PaymentProcessedEvent
                        {
                            BillingId = billing.Id,
                            AppointmentId = billing.AppointmentId,
                            PatientId = billing.PatientId,
                            Amount = billing.TotalAmount,
                            PaymentMethod = billing.PaymentMethod,
                            TransactionId = billing.TransactionId,
                            ProcessedAt = paymentResult.ProcessedAt,
                            ProcessedByUserId = currentUserId,
                            ProcessedByRole = currentUserRole
                        });
                    }
                }
                else
                {
                    await _rabbitMQService.PublishPaymentFailedAsync(new PaymentFailedEvent
                    {
                        BillingId = billing.Id,
                        AppointmentId = billing.AppointmentId,
                        PatientId = billing.PatientId,
                        Amount = billing.TotalAmount,
                        PaymentMethod = billing.PaymentMethod,
                        FailureReason = billing.FailureReason,
                        FailedAt = paymentResult.ProcessedAt,
                        ProcessedByUserId = currentUserId,
                        ProcessedByRole = currentUserRole
                    });
                }

                var resultDto = new PaymentResultDto
                {
                    IsSuccess = paymentResult.IsSuccess,
                    TransactionId = paymentResult.TransactionId,
                    FailureReason = paymentResult.FailureReason,
                    Amount = paymentResult.Amount,
                    ProcessedAt = paymentResult.ProcessedAt,
                    AdditionalData = paymentResult.AdditionalData // This will include checkout_url for Stripe
                };

                _logger.LogInformation("Payment processing completed for billing {BillingId}: {Success}",
                    id, paymentResult.IsSuccess ? "Success" : "Failed");

                return Ok(resultDto);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Unsupported payment method for billing {BillingId}", id);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for billing {BillingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/refund")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<ActionResult<PaymentResultDto>> RefundPayment(int id, RefundPaymentRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                var billing = await _billingRepository.GetByIdAsync(id);
                if (billing == null)
                {
                    return NotFound($"Billing with ID {id} not found");
                }

                if (billing.Status != "Completed")
                {
                    return BadRequest($"Cannot refund payment for billing with status: {billing.Status}");
                }

                if (string.IsNullOrEmpty(billing.TransactionId))
                {
                    return BadRequest("No transaction ID found for this billing");
                }

                var refundAmount = request.Amount ?? billing.Amount;
                if (refundAmount > billing.Amount)
                {
                    return BadRequest("Refund amount cannot exceed original payment amount");
                }

                _logger.LogInformation("Processing refund for billing {BillingId} amount {Amount}",
                    id, refundAmount);

                // Use factory to create payment method
                var paymentMethod = _paymentFactory.CreatePaymentMethod(billing.PaymentMethod);

                var refundResult = await paymentMethod.RefundPaymentAsync(billing.TransactionId, refundAmount);

                // Update billing status
                billing.Status = refundResult.IsSuccess ? "Refunded" : "Completed";
                billing.UpdatedAt = DateTime.UtcNow;

                await _billingRepository.UpdateAsync(billing);

                // Publish event to RabbitMQ
                if (refundResult.IsSuccess)
                {
                    await _rabbitMQService.PublishRefundProcessedAsync(new RefundProcessedEvent
                    {
                        BillingId = billing.Id,
                        AppointmentId = billing.AppointmentId,
                        PatientId = billing.PatientId,
                        OriginalAmount = billing.Amount,
                        RefundAmount = refundAmount,
                        PaymentMethod = billing.PaymentMethod,
                        OriginalTransactionId = billing.TransactionId,
                        RefundTransactionId = refundResult.TransactionId,
                        RefundedAt = refundResult.ProcessedAt,
                        RefundedByUserId = currentUserId,
                        RefundedByRole = currentUserRole
                    });
                }

                var resultDto = new PaymentResultDto
                {
                    IsSuccess = refundResult.IsSuccess,
                    TransactionId = refundResult.TransactionId,
                    FailureReason = refundResult.FailureReason,
                    Amount = refundResult.Amount,
                    ProcessedAt = refundResult.ProcessedAt,
                    AdditionalData = refundResult.AdditionalData
                };

                _logger.LogInformation("Refund processing completed for billing {BillingId}: {Success}",
                    id, refundResult.IsSuccess ? "Success" : "Failed");

                return Ok(resultDto);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Unsupported payment method for refund of billing {BillingId}", id);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for billing {BillingId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("payment-methods")]
        public ActionResult<IEnumerable<string>> GetAvailablePaymentMethods()
        {
            try
            {
                var paymentMethods = _paymentFactory.GetAvailablePaymentMethods();
                return Ok(paymentMethods);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available payment methods");
                return StatusCode(500, "Internal server error");
            }
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

        private bool CanAccessBilling(Billing billing, int userId, string userRole)
        {
            switch (userRole)
            {
                case "Admin":
                case "Doctor":
                    return true;

                case "Patient":
                    var userPatientId = GetCurrentUserPatientId();
                    return userPatientId == billing.PatientId;

                default:
                    return false;
            }
        }

        private BillingDto MapToBillingDto(Billing billing)
        {
            return new BillingDto
            {
                Id = billing.Id,
                AppointmentId = billing.AppointmentId,
                PatientId = billing.PatientId,
                Amount = billing.Amount,
                TotalAmount = billing.TotalAmount, 
                PaymentMethod = billing.PaymentMethod,
                Status = billing.Status,
                Description = billing.Description,
                TransactionId = billing.TransactionId,
                InvoiceNumber = billing.InvoiceNumber,
                BillingType = billing.BillingType,
                InsuranceNumber = billing.InsuranceNumber,
                CompanyId = billing.CompanyId,
                CreatedAt = billing.CreatedAt,
                UpdatedAt = billing.UpdatedAt
            };
        }
    }

    // DTOs
    public class CreateBillingRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int AppointmentId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int PatientId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        public string BillingType { get; set; } = string.Empty;
        public string? InsuranceNumber { get; set; }
        public string? CompanyId { get; set; }

        [Required]
        [StringLength(20)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class ProcessPaymentRequest
    {
        public string Currency { get; set; } = "USD";
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    public class RefundPaymentRequest
    {
        public decimal? Amount { get; set; }
    }

    public class BillingDto
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TransactionId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string BillingType { get; set; } = string.Empty;
        public string? InsuranceNumber { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PaymentResultDto
    {
        public bool IsSuccess { get; set; }
        public string? TransactionId { get; set; }
        public string? FailureReason { get; set; }
        public decimal Amount { get; set; }
        public DateTime ProcessedAt { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    

}

