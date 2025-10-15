using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.RabbitMQ;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace HospitalManagementSystem.Application.Services
{
    public class AccountantApplicationService
    {
        private readonly IMedicalRecordRepository _medicalRecordRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly HospitalDbContext _context;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;

        public AccountantApplicationService(
            IMedicalRecordRepository medicalRecordRepository, 
            IPaymentRepository paymentRepository, 
            IPatientRepository patientRepository,
            HospitalDbContext context, 
            IRabbitMQService rabbitMQService,
            IConfiguration configuration)
        {
            _medicalRecordRepository = medicalRecordRepository;
            _paymentRepository = paymentRepository;
            _patientRepository = patientRepository;
            _context = context;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
        }

        public async Task<IEnumerable<UnpaidMedicalRecordDto>> GetUnpaidMedicalRecordsAsync(int page, int pageSize)
        {
            var medicalRecords = await _medicalRecordRepository.GetUnpaidMedicalRecordsAsync(page, pageSize);
            var dtos = new List<UnpaidMedicalRecordDto>();

            foreach (var mr in medicalRecords)
            {
                var pendingPayment = await _paymentRepository.GetPendingPaymentByMedicalRecordIdAsync(mr.Id);
                dtos.Add(new UnpaidMedicalRecordDto
                {
                    MedicalRecordId = mr.Id,
                    AppointmentId = mr.AppointmentId,
                    AppointmentDate = mr.Appointment?.Date ?? default,
                    PatientName = mr.Patient?.Name ?? "N/A",
                    PatientId = mr.PatientId,
                    DoctorName = mr.Doctor?.Name ?? "N/A",
                    TotalFee = mr.TotalFee,
                    PaidAmount = mr.PaidAmount,
                    RemainingAmount = mr.RemainingAmount,
                    PaymentStatus = mr.PaymentStatus,
                    PendingPaymentId = pendingPayment?.Id,
                    PendingPaymentMethod = pendingPayment?.PaymentMethod
                });
            }
            return dtos;
        }

        public async Task<PaymentDto> InitiateCashPaymentForMedicalRecordAsync(int medicalRecordId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(medicalRecordId);
                if (medicalRecord == null || medicalRecord.PaymentStatus != "Unpaid")
                {
                    throw new InvalidOperationException("Medical record is not available for payment.");
                }

                var existingPendingPayment = await _paymentRepository.GetPendingPaymentByMedicalRecordIdAsync(medicalRecordId);
                if (existingPendingPayment != null)
                {
                    throw new InvalidOperationException($"A pending payment (ID: {existingPendingPayment.Id}) already exists for this medical record.");
                }

                var payment = new Payment
                {
                    PatientId = medicalRecord.PatientId,
                    MedicalRecordId = medicalRecord.Id,
                    Amount = medicalRecord.RemainingAmount, // Use RemainingAmount
                    PaymentType = PaymentTypes.FinalPayment,
                    PaymentMethod = "Cash",
                    Status = PaymentStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var createdPayment = await _paymentRepository.CreateAsync(payment);
                await transaction.CommitAsync();

                return new PaymentDto
                {
                    Id = createdPayment.Id,
                    MedicalRecordId = createdPayment.MedicalRecordId,
                    Amount = createdPayment.Amount,
                    PaymentType = createdPayment.PaymentType,
                    PaymentMethod = createdPayment.PaymentMethod,
                    Status = createdPayment.Status,
                    CreatedAt = createdPayment.CreatedAt
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<InitiateStripePaymentResponseDto> InitiateStripePaymentForMedicalRecordAsync(int medicalRecordId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(medicalRecordId);
                if (medicalRecord == null || medicalRecord.PaymentStatus != "Unpaid")
                {
                    throw new InvalidOperationException("Medical record is not available for payment.");
                }

                var existingPendingPayment = await _paymentRepository.GetPendingPaymentByMedicalRecordIdAsync(medicalRecordId);
                if (existingPendingPayment != null && !string.IsNullOrEmpty(existingPendingPayment.StripeSessionId))
                {
                    var sessionService = new SessionService();
                    try
                    {
                        var session = await sessionService.GetAsync(existingPendingPayment.StripeSessionId);
                        if (session.Status == "open" && session.ExpiresAt > DateTime.UtcNow)
                        {
                            await transaction.CommitAsync(); // No changes, but commit to end transaction
                            return new InitiateStripePaymentResponseDto
                            {
                                PaymentId = existingPendingPayment.Id,
                                StripeCheckoutUrl = session.Url,
                                ExpiresAt = session.ExpiresAt
                            };
                        }
                        else
                        {
                            // Mark old payment as failed and continue to create a new one
                            existingPendingPayment.Status = PaymentStatuses.Failed;
                            existingPendingPayment.FailureReason = "Stripe session expired or closed.";
                            await _paymentRepository.UpdateAsync(existingPendingPayment);
                        }
                    }
                    catch (StripeException ex)
                    {
                        // Session not found on Stripe, mark as failed and continue
                        existingPendingPayment.Status = PaymentStatuses.Failed;
                        existingPendingPayment.FailureReason = $"Stripe session not found: {ex.Message}";
                        await _paymentRepository.UpdateAsync(existingPendingPayment);
                    }
                }

                var patient = await _patientRepository.GetPatientByIdAsync(medicalRecord.PatientId);
                if (patient == null)
                {
                    throw new InvalidOperationException("Patient not found for the medical record.");
                }

                var payment = new Payment
                {
                    PatientId = medicalRecord.PatientId,
                    MedicalRecordId = medicalRecord.Id,
                    Amount = medicalRecord.RemainingAmount,
                    PaymentType = PaymentTypes.FinalPayment,
                    PaymentMethod = "Stripe",
                    Status = PaymentStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var createdPayment = await _paymentRepository.CreateAsync(payment);

                var expiresAt = DateTime.UtcNow.AddMinutes(30);
                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";
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
                                    Name = $"Hospital Fee for Medical Record #{medicalRecord.Id}",
                                    Description = $"Payment for services related to patient {patient.Name} on {medicalRecord.Appointment.Date:yyyy-MM-dd}",
                                },
                                UnitAmount = (long)createdPayment.Amount,
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    ExpiresAt = expiresAt,
                    SuccessUrl = $"{baseUrl}/payment-success.html",
                    CancelUrl = $"{baseUrl}/payment-cancelled.html",
                    CustomerEmail = patient.Email,
                    Metadata = new Dictionary<string, string>
                    {
                        { "payment_id", createdPayment.Id.ToString() },
                        { "medical_record_id", medicalRecord.Id.ToString() },
                        { "payment_type", PaymentTypes.FinalPayment }
                    }
                };

                var service = new SessionService();
                var newSession = await service.CreateAsync(options);

                createdPayment.StripeSessionId = newSession.Id;
                await _paymentRepository.UpdateAsync(createdPayment);

                await transaction.CommitAsync();

                return new InitiateStripePaymentResponseDto
                {
                    PaymentId = createdPayment.Id,
                    StripeCheckoutUrl = newSession.Url,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> ConfirmCashPaymentAsync(int paymentId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.Status != PaymentStatuses.Pending || payment.PaymentMethod != "Cash" || !payment.MedicalRecordId.HasValue)
                {
                    return false;
                }

                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(payment.MedicalRecordId.Value);
                if (medicalRecord == null || medicalRecord.PaymentStatus != "Unpaid")
                {
                    return false;
                }

                payment.Status = PaymentStatuses.Completed;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                medicalRecord.PaymentStatus = "Paid";
                medicalRecord.PaidAmount += payment.Amount;
                await _medicalRecordRepository.UpdateAsync(medicalRecord);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CancelCashPaymentAsync(int paymentId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.Status != PaymentStatuses.Pending || payment.PaymentMethod != "Cash")
                {
                    return false;
                }

                payment.Status = PaymentStatuses.Failed;
                payment.FailureReason = "Cancelled by accountant";
                await _paymentRepository.UpdateAsync(payment);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CancelStripePaymentAsync(int paymentId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.Status != PaymentStatuses.Pending || payment.PaymentMethod != "Stripe")
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(payment.StripeSessionId))
                {
                    var service = new SessionService();
                    await service.ExpireAsync(payment.StripeSessionId);
                }

                payment.Status = PaymentStatuses.Failed;
                payment.FailureReason = "Cancelled by accountant";
                await _paymentRepository.UpdateAsync(payment);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

