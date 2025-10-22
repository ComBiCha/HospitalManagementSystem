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
using System.Text.Json;
using HospitalManagementSystem.Domain.Caching;
using Microsoft.Extensions.Logging;
using HospitalManagementSystem.Application.DTOs.Accountant;
using HospitalManagementSystem.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Domain.Payments;

namespace HospitalManagementSystem.Application.Services
{
    public class AccountantApplicationService
    {
        private readonly IMedicalRecordRepository _medicalRecordRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly HospitalDbContext _context;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AccountantApplicationService> _logger;
        private readonly IStripePaymentService _stripePaymentService;

        public AccountantApplicationService(
            IMedicalRecordRepository medicalRecordRepository, 
            IPaymentRepository paymentRepository, 
            IPatientRepository patientRepository,
            IAppointmentRepository appointmentRepository,
            HospitalDbContext context, 
            IRabbitMQService rabbitMQService,
            IConfiguration configuration,
            ICacheService cacheService,
            ILogger<AccountantApplicationService> logger,
            IStripePaymentService stripePaymentService)
        {
            _medicalRecordRepository = medicalRecordRepository;
            _paymentRepository = paymentRepository;
            _patientRepository = patientRepository;
            _appointmentRepository = appointmentRepository;
            _context = context;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
            _cacheService = cacheService;
            _logger = logger;
            _stripePaymentService = stripePaymentService;
        }

        #region Eligible Appointments for Deposit

        public async Task<PaginatedResultDto<EligibleAppointmentDto>> GetEligibleForDepositAppointmentsAsync(AccountantFilterDto filter, int page, int pageSize)
        {
            ISpecification<Appointment> spec = new EligibleForDepositSpecification();

            if (!string.IsNullOrEmpty(filter.PatientName))
            {
                spec = spec.And(new AppointmentByPatientNameSpecification(filter.PatientName));
            }

            if (filter.AppointmentId.HasValue)
            {
                spec = spec.And(new AppointmentByIdSpecification(filter.AppointmentId.Value));
            }

            if (filter.StartDate.HasValue)
            {
                var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
                var endDateUtc = (filter.EndDate.HasValue ? filter.EndDate.Value.Date : DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);
                endDateUtc = DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc);
                spec = spec.And(new AppointmentByDateRangeSpecification(startDateUtc, endDateUtc));
            }

            var query = _appointmentRepository.GetQueryable(spec);
            var totalCount = await query.CountAsync();

            var appointments = await query
                .OrderByDescending(a => a.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = new List<EligibleAppointmentDto>();

            foreach (var appointment in appointments)
            {
                var dto = new EligibleAppointmentDto
                {
                    AppointmentId = appointment.Id,
                    AppointmentDate = appointment.Date,
                    PatientName = appointment.Patient?.Name ?? "N/A",
                    PatientId = appointment.PatientId,
                    DoctorName = appointment.Doctor?.Name ?? "N/A",
                    AppointmentStatus = appointment.Status
                };

                var pendingPayment = await _paymentRepository.GetPendingDepositByAppointmentIdAsync(appointment.Id);
                if (pendingPayment != null)
                {
                    dto.PendingPaymentId = pendingPayment.Id;
                    dto.PendingPaymentMethod = pendingPayment.PaymentMethod;
                    dto.PendingPaymentStatus = pendingPayment.Status;

                    if (pendingPayment.PaymentMethod == "Stripe" && !string.IsNullOrEmpty(pendingPayment.StripeSessionId))
                    {
                        try
                        {
                            var sessionService = new SessionService();
                            var session = await sessionService.GetAsync(pendingPayment.StripeSessionId);
                            if (session.Status == "open")
                            {
                                dto.StripeCheckoutUrl = session.Url;
                                dto.StripeSessionExpiresAt = session.ExpiresAt;
                            }
                        }
                        catch (StripeException) 
                        {
                            // Session is likely expired or invalid, ignore and leave URL null
                        }
                    }
                }
                dtos.Add(dto);
            }

            return new PaginatedResultDto<EligibleAppointmentDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        #endregion

        #region Advance Payment
        public async Task<decimal> GetAdvancePaymentSuggestionAsync(int appointmentId)
        {
            var appointment = await _appointmentRepository.GetByIdWithDetailsAsync(appointmentId);
            if (appointment?.Doctor == null)
            {
                throw new KeyNotFoundException("Appointment or Doctor not found.");
            }

            var specialty = appointment.Doctor.Specialty;
            var suggestions = _configuration.GetSection("AdvancePaymentSuggestions").Get<Dictionary<string, decimal>>();

            if (suggestions != null && suggestions.TryGetValue(specialty, out var amount))
            {
                return amount;
            }
            
            if (suggestions != null && suggestions.TryGetValue("Default", out var defaultAmount))
            {
                return defaultAmount;
            }

            return 0;
        }

        public async Task<PaymentDto> InitiateAdvanceCashPaymentAsync(int appointmentId)
        {
            var existingPendingPayment = await _paymentRepository.GetPendingDepositByAppointmentIdAsync(appointmentId);
            if (existingPendingPayment != null)
            {
                throw new InvalidOperationException($"An advance payment (ID: {existingPendingPayment.Id}, Method: {existingPendingPayment.PaymentMethod}) is already pending for this appointment.");
            }

            var suggestedAmount = await GetAdvancePaymentSuggestionAsync(appointmentId);
            if (suggestedAmount <= 0)
            {
                throw new InvalidOperationException("No advance payment suggestion found for this appointment's specialty.");
            }

            return await InitiateAdvancePaymentAsync(appointmentId, "Cash", suggestedAmount);
        }

        public async Task<InitiateStripePaymentResponseDto> InitiateAdvanceStripePaymentAsync(int appointmentId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingPendingPayment = await _paymentRepository.GetPendingDepositByAppointmentIdAsync(appointmentId);
                if (existingPendingPayment != null)
                {
                    if (existingPendingPayment.PaymentMethod == "Cash")
                    {
                        throw new InvalidOperationException("A cash payment is already pending. Please confirm or cancel it first.");
                    }

                    if (existingPendingPayment.PaymentMethod == "Stripe" && !string.IsNullOrEmpty(existingPendingPayment.StripeSessionId))
                    {
                        var sessionService = new SessionService();
                        try
                        {
                            var session = await sessionService.GetAsync(existingPendingPayment.StripeSessionId);
                            if (session.Status == "open" && session.ExpiresAt > DateTime.UtcNow)
                            {
                                await transaction.CommitAsync();
                                return new InitiateStripePaymentResponseDto
                                {
                                    PaymentId = existingPendingPayment.Id,
                                    StripeCheckoutUrl = session.Url,
                                    ExpiresAt = session.ExpiresAt
                                };
                            }
                            else
                            {
                                existingPendingPayment.Status = PaymentStatuses.Failed;
                                existingPendingPayment.FailureReason = "Stripe session expired or was closed. Creating a new one.";
                                await _paymentRepository.UpdateAsync(existingPendingPayment);
                            }
                        }
                        catch (StripeException ex)
                        {
                            existingPendingPayment.Status = PaymentStatuses.Failed;
                            existingPendingPayment.FailureReason = $"Stripe session not found, creating a new one: {ex.Message}";
                            await _paymentRepository.UpdateAsync(existingPendingPayment);
                        }
                    }
                }

                var suggestedAmount = await GetAdvancePaymentSuggestionAsync(appointmentId);
                if (suggestedAmount <= 0)
                {
                    throw new InvalidOperationException("No advance payment suggestion found for this appointment's specialty.");
                }

                var paymentDto = await InitiateAdvancePaymentAsync(appointmentId, "Stripe", suggestedAmount, false);

                var patient = await _patientRepository.GetPatientByIdAsync(paymentDto.PatientId);
                var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";
                var metadata = new Dictionary<string, string>
                {
                    { "payment_id", paymentDto.Id.ToString() },
                    { "appointment_id", appointmentId.ToString() },
                    { "payment_type", PaymentTypes.Deposit }
                };

                var newSession = await _stripePaymentService.CreateCheckoutSessionAsync(
                    (long)paymentDto.Amount,
                    $"Advance Payment for Appointment #{appointmentId}",
                    $"Advance payment for appointment with Dr. {appointment.Doctor.Name} on {appointment.Date:yyyy-MM-dd}",
                    patient.Email,
                    $"{baseUrl}/payment-success.html",
                    $"{baseUrl}/payment-cancelled.html",
                    metadata
                );

                var paymentToUpdate = await _paymentRepository.GetByIdAsync(paymentDto.Id);
                paymentToUpdate.StripeSessionId = newSession.Id;
                await _paymentRepository.UpdateAsync(paymentToUpdate);

                await transaction.CommitAsync();

                return new InitiateStripePaymentResponseDto
                {
                    PaymentId = paymentDto.Id,
                    StripeCheckoutUrl = newSession.Url,
                    ExpiresAt = newSession.ExpiresAt
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<PaymentDto> InitiateAdvancePaymentAsync(int appointmentId, string paymentMethod, decimal amount, bool useTransaction = true)
        {
            var transaction = useTransaction ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
                if (appointment == null)
                {
                    throw new KeyNotFoundException("Appointment not found.");
                }

                if (appointment.Status == "Completed" || appointment.Status == "Cancelled" || appointment.Status == "ExpiredPayment")
                {
                    throw new InvalidOperationException($"Appointment with status '{appointment.Status}' is not eligible for an advance payment.");
                }

                var payment = new Payment
                {
                    PatientId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    Amount = amount,
                    PaymentType = PaymentTypes.Deposit,
                    PaymentMethod = paymentMethod,
                    Status = PaymentStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var createdPayment = await _paymentRepository.CreateAsync(payment);
                if (transaction != null) 
                {
                    await transaction.CommitAsync();
                }

                return new PaymentDto
                {
                    Id = createdPayment.Id,
                    PatientId = createdPayment.PatientId,
                    AppointmentId = createdPayment.AppointmentId,
                    Amount = createdPayment.Amount,
                    PaymentType = createdPayment.PaymentType,
                    PaymentMethod = createdPayment.PaymentMethod,
                    Status = createdPayment.Status,
                    CreatedAt = createdPayment.CreatedAt
                };
            }
            catch (Exception)
            {
                if (transaction != null) 
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        #endregion

        #region Final Payment
        public async Task<PaginatedResultDto<UnpaidMedicalRecordDto>> GetUnpaidMedicalRecordsAsync(AccountantFilterDto filter, int page, int pageSize)
        {
            ISpecification<MedicalRecord> spec = new UnpaidMedicalRecordSpecification();

            if (!string.IsNullOrEmpty(filter.PatientName))
            {
                spec = spec.And(new MedicalRecordByPatientNameSpecification(filter.PatientName));
            }

            if (filter.MedicalRecordId.HasValue)
            {
                spec = spec.And(new MedicalRecordByIdSpecification(filter.MedicalRecordId.Value));
            }

            if (filter.StartDate.HasValue)
            {
                var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
                var endDateUtc = (filter.EndDate.HasValue ? filter.EndDate.Value.Date : filter.StartDate.Value.Date).AddDays(1).AddTicks(-1);
                endDateUtc = DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc);
                spec = spec.And(new MedicalRecordByDateRangeSpecification(startDateUtc, endDateUtc));
            }

            var query = _medicalRecordRepository.GetQueryable(spec);
            var totalCount = await query.CountAsync();

            var medicalRecords = await query
                .OrderByDescending(mr => mr.Appointment.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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

            return new PaginatedResultDto<UnpaidMedicalRecordDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
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
                    Amount = medicalRecord.RemainingAmount, 
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

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";
                var metadata = new Dictionary<string, string>
                {
                    { "payment_id", createdPayment.Id.ToString() },
                    { "medical_record_id", medicalRecord.Id.ToString() },
                    { "payment_type", PaymentTypes.FinalPayment }
                };

                var newSession = await _stripePaymentService.CreateCheckoutSessionAsync(
                    (long)createdPayment.Amount,
                    $"Hospital Fee for Medical Record #{medicalRecord.Id}",
                    $"Payment for services related to patient {patient.Name} on {medicalRecord.Appointment.Date:yyyy-MM-dd}",
                    patient.Email,
                    $"{baseUrl}/payment-success.html",
                    $"{baseUrl}/payment-cancelled.html",
                    metadata
                );

                createdPayment.StripeSessionId = newSession.Id;
                await _paymentRepository.UpdateAsync(createdPayment);

                await transaction.CommitAsync();

                return new InitiateStripePaymentResponseDto
                {
                    PaymentId = createdPayment.Id,
                    StripeCheckoutUrl = newSession.Url,
                    ExpiresAt = newSession.ExpiresAt
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        #endregion

        #region Payment Confirmation and Cancellation
        public async Task<bool> ConfirmCashPaymentAsync(int paymentId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.Status != PaymentStatuses.Pending || payment.PaymentMethod != "Cash")
                {
                    return false;
                }

                payment.Status = PaymentStatuses.Completed;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                int? medicalRecordId = payment.MedicalRecordId;

                if (!medicalRecordId.HasValue && payment.PaymentType == PaymentTypes.Deposit && payment.AppointmentId.HasValue)
                {
                    var medicalRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(payment.AppointmentId.Value);
                    if (medicalRecord != null)
                    {
                        medicalRecordId = medicalRecord.Id;
                    }
                }

                if (medicalRecordId.HasValue)
                {
                    var medicalRecord = await _medicalRecordRepository.GetByIdAsync(medicalRecordId.Value);
                    if (medicalRecord != null)
                    {
                        var completedDeposits = await _paymentRepository.GetCompletedDepositsByAppointmentIdAsync(medicalRecord.AppointmentId);
                        var completedMedicalPayments = await _paymentRepository.GetCompletedPaymentsByMedicalRecordIdAsync(medicalRecord.Id);
                        var allPayments = completedDeposits.Union(completedMedicalPayments).DistinctBy(p => p.Id);
                        medicalRecord.PaidAmount = allPayments.Sum(p => p.Amount);

                        if (payment.PaymentType == PaymentTypes.FinalPayment && medicalRecord.PaidAmount >= medicalRecord.TotalFee)
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
                        _logger.LogInformation("Cleared medical record cache for patient {PatientId} due to cash payment confirmation.", medicalRecord.PatientId);
                    }
                }

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
        #endregion

        #region Refund Management

        public async Task<PaginatedResultDto<RefundableMedicalRecordDto>> GetRefundableMedicalRecordsAsync(AccountantFilterDto filter, int page, int pageSize)
        {
            ISpecification<MedicalRecord> spec = new RefundableMedicalRecordSpecification();

            if (!string.IsNullOrEmpty(filter.PatientName))
            {
                spec = spec.And(new MedicalRecordByPatientNameSpecification(filter.PatientName));
            }

            if (filter.MedicalRecordId.HasValue)
            {
                spec = spec.And(new MedicalRecordByIdSpecification(filter.MedicalRecordId.Value));
            }

            if (filter.StartDate.HasValue)
            {
                var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
                var endDateUtc = (filter.EndDate.HasValue ? filter.EndDate.Value.Date : filter.StartDate.Value.Date).AddDays(1).AddTicks(-1);
                endDateUtc = DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc);
                spec = spec.And(new MedicalRecordByDateRangeSpecification(startDateUtc, endDateUtc));
            }

            var query = _medicalRecordRepository.GetQueryable(spec);
            var totalCount = await query.CountAsync();

            var medicalRecords = await query
                .OrderByDescending(mr => mr.Appointment.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = new List<RefundableMedicalRecordDto>();
            foreach (var mr in medicalRecords)
            {
                var refundPayment = await _paymentRepository.GetPendingPaymentByMedicalRecordIdAsync(mr.Id);

                dtos.Add(new RefundableMedicalRecordDto
                {
                    MedicalRecordId = mr.Id,
                    AppointmentId = mr.AppointmentId,
                    PatientName = mr.Patient.Name,
                    PatientId = mr.PatientId,
                    DoctorName = mr.Doctor.Name,
                    TotalFee = mr.TotalFee,
                    PaidAmount = mr.PaidAmount,
                    OverpaidAmount = mr.PaidAmount - mr.TotalFee,
                    RefundPaymentId = refundPayment?.Id,
                    RefundPaymentStatus = refundPayment?.Status
                });
            }

            return new PaginatedResultDto<RefundableMedicalRecordDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PaymentDto> InitiateRefundPaymentAsync(int medicalRecordId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var medicalRecord = await _medicalRecordRepository.GetByIdAsync(medicalRecordId);
                if (medicalRecord == null || medicalRecord.PaidAmount <= medicalRecord.TotalFee)
                {
                    throw new InvalidOperationException("Medical record is not eligible for a refund.");
                }

                var existingRefund = await _paymentRepository.GetPendingPaymentByMedicalRecordIdAsync(medicalRecordId);
                if (existingRefund != null)
                {
                    throw new InvalidOperationException($"A refund payment (ID: {existingRefund.Id}) is already pending for this medical record.");
                }

                var refundAmount = medicalRecord.TotalFee - medicalRecord.PaidAmount; // This will be a negative number

                var payment = new Payment
                {
                    PatientId = medicalRecord.PatientId,
                    MedicalRecordId = medicalRecord.Id,
                    AppointmentId = medicalRecord.AppointmentId,
                    Amount = refundAmount,
                    PaymentType = PaymentTypes.FinalPayment, // As requested
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

        public async Task<bool> CompleteRefundPaymentAsync(int paymentId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.Status != PaymentStatuses.Pending || payment.Amount >= 0)
                {
                    return false; // Not a pending refund payment
                }

                payment.Status = PaymentStatuses.Completed;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment);

                if (payment.MedicalRecordId.HasValue)
                {
                    var medicalRecord = await _medicalRecordRepository.GetByIdAsync(payment.MedicalRecordId.Value);
                    if (medicalRecord != null)
                    {
                        var completedDeposits = await _paymentRepository.GetCompletedDepositsByAppointmentIdAsync(medicalRecord.AppointmentId);
                        var completedMedicalPayments = await _paymentRepository.GetCompletedPaymentsByMedicalRecordIdAsync(medicalRecord.Id);
                        var allPayments = completedDeposits.Union(completedMedicalPayments).DistinctBy(p => p.Id);
                        medicalRecord.PaidAmount = allPayments.Sum(p => p.Amount);

                        // After refund, PaidAmount should equal TotalFee, so we can mark as Paid
                        if (Math.Abs(medicalRecord.PaidAmount - medicalRecord.TotalFee) < 0.01m)
                        {
                            medicalRecord.PaymentStatus = "Paid";
                        }
                        await _medicalRecordRepository.UpdateAsync(medicalRecord);

                        // Invalidate cache for the patient's medical records
                        await _cacheService.RemovePatternAsync($"patient:{medicalRecord.PatientId}:medical-records:*");
                        _logger.LogInformation("Cleared medical record cache for patient {PatientId} due to refund completion.", medicalRecord.PatientId);
                    }
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion
    }
}
