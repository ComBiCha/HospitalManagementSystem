using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.API.Grpc;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Payments;
using HospitalManagementSystem.Domain.Factories;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.API.Protos;

namespace HospitalManagementSystem.API.Services
{
    [Authorize]
    public class BillingGrpcService : Protos.BillingGrpcService.BillingGrpcServiceBase
    {
        private readonly IBillingRepository _billingRepository;
        private readonly IPaymentFactory _paymentFactory;
        private readonly ILogger<BillingGrpcService> _logger;

        public BillingGrpcService(
            IBillingRepository billingRepository,
            IPaymentFactory paymentFactory,
            ILogger<BillingGrpcService> logger)
        {
            _billingRepository = billingRepository;
            _paymentFactory = paymentFactory;
            _logger = logger;
        }

        public override async Task<BillingResponse> GetBilling(GetBillingRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetBilling called for ID: {BillingId}", request.BillingId);

                var billing = await _billingRepository.GetByIdAsync(request.BillingId);
                if (billing == null)
                {
                    return new BillingResponse
                    {
                        Success = false,
                        Message = $"Billing with ID {request.BillingId} not found"
                    };
                }

                return new BillingResponse
                {
                    Success = true,
                    Message = "Billing retrieved successfully",
                    Billing = MapToBillingDto(billing)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetBilling for ID: {BillingId}", request.BillingId);
                return new BillingResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override async Task<BillingResponse> GetBillingByAppointment(GetBillingByAppointmentRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetBillingByAppointment called for AppointmentId: {AppointmentId}", request.AppointmentId);

                var billing = await _billingRepository.GetByAppointmentIdAsync(request.AppointmentId);
                if (billing == null)
                {
                    return new BillingResponse
                    {
                        Success = false,
                        Message = $"No billing found for appointment {request.AppointmentId}"
                    };
                }

                return new BillingResponse
                {
                    Success = true,
                    Message = "Billing retrieved successfully",
                    Billing = MapToBillingDto(billing)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetBillingByAppointment for AppointmentId: {AppointmentId}", request.AppointmentId);
                return new BillingResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override async Task<BillingsResponse> GetBillingsByPatient(GetBillingsByPatientRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetBillingsByPatient called for PatientId: {PatientId}", request.PatientId);

                var billings = await _billingRepository.GetByPatientIdAsync(request.PatientId);
                var billingDtos = billings.Select(MapToBillingDto);

                return new BillingsResponse
                {
                    Success = true,
                    Message = "Billings retrieved successfully",
                    Billings = { billingDtos }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetBillingsByPatient for PatientId: {PatientId}", request.PatientId);
                return new BillingsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override async Task<BillingResponse> CreateBilling(CreateBillingRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC CreateBilling called for AppointmentId: {AppointmentId}", request.AppointmentId);

                // Check if billing already exists
                var existingBilling = await _billingRepository.GetByAppointmentIdAsync(request.AppointmentId);
                if (existingBilling != null)
                {
                    return new BillingResponse
                    {
                        Success = false,
                        Message = $"Billing already exists for appointment {request.AppointmentId}"
                    };
                }

                var billing = new Billing
                {
                    AppointmentId = request.AppointmentId,
                    PatientId = request.PatientId,
                    Amount = (decimal)request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    Status = "Pending",
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdBilling = await _billingRepository.CreateAsync(billing);

                return new BillingResponse
                {
                    Success = true,
                    Message = "Billing created successfully",
                    Billing = MapToBillingDto(createdBilling)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC CreateBilling");
                return new BillingResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override async Task<PaymentResultResponse> ProcessPayment(ProcessPaymentRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC ProcessPayment called for BillingId: {BillingId}", request.BillingId);

                var billing = await _billingRepository.GetByIdAsync(request.BillingId);
                if (billing == null)
                {
                    return new PaymentResultResponse
                    {
                        Success = false,
                        Message = $"Billing with ID {request.BillingId} not found"
                    };
                }

                if (billing.Status != "Pending")
                {
                    return new PaymentResultResponse
                    {
                        Success = false,
                        Message = $"Cannot process payment for billing with status: {billing.Status}"
                    };
                }

                // Use factory to create payment method
                var paymentMethod = _paymentFactory.CreatePaymentMethod(billing.PaymentMethod);

                var paymentRequest = new PaymentRequest
                {
                    Amount = billing.Amount,
                    Currency = request.Currency ?? "USD",
                    Description = billing.Description ?? $"Payment for appointment {billing.AppointmentId}",
                    PatientId = billing.PatientId,
                    AppointmentId = billing.AppointmentId,
                    AdditionalData = request.AdditionalData?.ToDictionary(x => x.Key, x => (object)x.Value) ?? new Dictionary<string, object>()
                };

                var paymentResult = await paymentMethod.ProcessPaymentAsync(paymentRequest);

                // Update billing
                billing.Status = paymentResult.IsSuccess ? "Completed" : "Failed";
                billing.TransactionId = paymentResult.TransactionId;
                billing.FailureReason = paymentResult.FailureReason;
                billing.UpdatedAt = DateTime.UtcNow;

                await _billingRepository.UpdateAsync(billing);

                return new PaymentResultResponse
                {
                    Success = true,
                    Message = "Payment processed successfully",
                    PaymentResult = new PaymentResultDto
                    {
                        IsSuccess = paymentResult.IsSuccess,
                        TransactionId = paymentResult.TransactionId ?? string.Empty,
                        FailureReason = paymentResult.FailureReason ?? string.Empty,
                        Amount = (double)paymentResult.Amount,
                        ProcessedAt = paymentResult.ProcessedAt.ToString("O"),
                        AdditionalData = { paymentResult.AdditionalData?.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>() }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC ProcessPayment for BillingId: {BillingId}", request.BillingId);
                return new PaymentResultResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override async Task<PaymentResultResponse> RefundPayment(RefundPaymentRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC RefundPayment called for BillingId: {BillingId}", request.BillingId);

                var billing = await _billingRepository.GetByIdAsync(request.BillingId);
                if (billing == null)
                {
                    return new PaymentResultResponse
                    {
                        Success = false,
                        Message = $"Billing with ID {request.BillingId} not found"
                    };
                }

                if (billing.Status != "Completed")
                {
                    return new PaymentResultResponse
                    {
                        Success = false,
                        Message = $"Cannot refund payment for billing with status: {billing.Status}"
                    };
                }

                // Fix: Handle nullable double properly
                var refundAmount = request.HasAmount ? (decimal)request.Amount : billing.Amount;

                // Use factory to create payment method
                var paymentMethod = _paymentFactory.CreatePaymentMethod(billing.PaymentMethod);
                var refundResult = await paymentMethod.RefundPaymentAsync(billing.TransactionId!, refundAmount);

                // Update billing status
                billing.Status = refundResult.IsSuccess ? "Refunded" : "Completed";
                billing.UpdatedAt = DateTime.UtcNow;

                await _billingRepository.UpdateAsync(billing);

                return new PaymentResultResponse
                {
                    Success = true,
                    Message = "Refund processed successfully",
                    PaymentResult = new PaymentResultDto
                    {
                        IsSuccess = refundResult.IsSuccess,
                        TransactionId = refundResult.TransactionId ?? string.Empty,
                        FailureReason = refundResult.FailureReason ?? string.Empty,
                        Amount = (double)refundResult.Amount,
                        ProcessedAt = refundResult.ProcessedAt.ToString("O"),
                        AdditionalData = { refundResult.AdditionalData?.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>() }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC RefundPayment for BillingId: {BillingId}", request.BillingId);
                return new PaymentResultResponse
                {
                    Success = false,
                    Message = "Internal server error"
                };
            }
        }

        public override Task<PaymentMethodsResponse> GetAvailablePaymentMethods(GetPaymentMethodsRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("gRPC GetAvailablePaymentMethods called");

                var paymentMethods = _paymentFactory.GetAvailablePaymentMethods();

                return Task.FromResult(new PaymentMethodsResponse
                {
                    Success = true,
                    Message = "Payment methods retrieved successfully",
                    PaymentMethods = { paymentMethods }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetAvailablePaymentMethods");
                return Task.FromResult(new PaymentMethodsResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        private static BillingDto MapToBillingDto(Billing billing)
        {
            return new BillingDto
            {
                Id = billing.Id,
                AppointmentId = billing.AppointmentId,
                PatientId = billing.PatientId,
                Amount = (double)billing.Amount,
                PaymentMethod = billing.PaymentMethod,
                Status = billing.Status,
                Description = billing.Description ?? string.Empty,
                TransactionId = billing.TransactionId ?? string.Empty,
                CreatedAt = billing.CreatedAt.ToString("O"),
                UpdatedAt = billing.UpdatedAt.ToString("O")
            };
        }
    }
}
