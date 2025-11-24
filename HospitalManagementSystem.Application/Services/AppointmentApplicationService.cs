
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Application.DTOs.Appointment;
using HospitalManagementSystem.Application.DTOs.Common;
using HospitalManagementSystem.Domain.Specifications;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync, CountAsync
using System.Linq;
using Hangfire;
using HospitalManagementSystem.Domain.Events;
using HospitalManagementSystem.Domain.Payments;
using HospitalManagementSystem.Domain.RabbitMQ;
using Microsoft.Extensions.Logging;
using Stripe;

namespace HospitalManagementSystem.Application.Services
{
    public class AppointmentApplicationService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly IDoctorShiftRepository _doctorShiftRepository;
        private readonly IAuthRepository _authRepository;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IStripePaymentService _stripePaymentService;
        private readonly ILogger<AppointmentApplicationService> _logger;
        private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        public AppointmentApplicationService(
            IAppointmentRepository appointmentRepository, 
            IDoctorRepository doctorRepository, 
            IDoctorShiftRepository doctorShiftRepository,
            IAuthRepository authRepository,
            IBackgroundJobClient backgroundJobClient,
            IRabbitMQService rabbitMQService,
            IPaymentRepository paymentRepository,
            IStripePaymentService stripePaymentService,
            ILogger<AppointmentApplicationService> logger)
        {
            _appointmentRepository = appointmentRepository;
            _doctorRepository = doctorRepository;
            _doctorShiftRepository = doctorShiftRepository;
            _authRepository = authRepository;
            _backgroundJobClient = backgroundJobClient;
            _rabbitMQService = rabbitMQService;
            _paymentRepository = paymentRepository;
            _stripePaymentService = stripePaymentService;
            _logger = logger;
        }

        public async Task<PagedResult<Appointment>> GetAppointmentsForPatientAsync(int patientId, AppointmentFilterDto filter, int page, int pageSize)
        {
            ISpecification<Appointment> spec = new AppointmentForPatientSpecification(patientId);

            if (!string.IsNullOrEmpty(filter.Status))
            {
                spec = spec.And(new AppointmentByStatusSpecification(filter.Status));
            }

            if (filter.StartDate.HasValue && filter.EndDate.HasValue)
            {
                var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
                var endDateValue = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
                var endDateUtc = DateTime.SpecifyKind(endDateValue, DateTimeKind.Utc);

                spec = spec.And(new AppointmentByDateRangeSpecification(startDateUtc, endDateUtc));
            }

            var query = _appointmentRepository.GetQueryable(spec);
            var totalCount = await query.CountAsync();

            var appointments = await query
                .OrderByDescending(a => a.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Appointment>(appointments, page, pageSize, totalCount);
        }

        public async Task<IEnumerable<string>> GetSpecialtiesAsync()
        {
            var doctors = await _doctorRepository.GetAllAsync();
            return doctors
                .Where(d => d.Status.HasFlag(HospitalManagementSystem.Domain.Entities.DoctorStatus.Active))
                .Select(d => d.Specialty)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        public async Task<IEnumerable<AvailableDoctorDto>> GetDoctorsBySpecialtyAsync(string specialty)
        {
            var doctors = await _doctorRepository.GetBySpecialtyAsync(specialty);
            return doctors
                .Where(d => d.Status.HasFlag(HospitalManagementSystem.Domain.Entities.DoctorStatus.Active))
                .Select(d => new AvailableDoctorDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Specialty = d.Specialty,
                    Email = d.Email
                }).ToList();
        }

        public async Task<IEnumerable<TimeSlotDto>> GetAvailableTimeSlotsAsync(DateTime date)
        {
            var timeSlots = new List<TimeSlotDto>();
            var hasWorkingDoctor = await _doctorShiftRepository.HasAnyActiveShiftOnDateAsync(date);

            if (hasWorkingDoctor)
            {
                var minBookingTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone).AddHours(6);
                for (int hour = 8; hour <= 16; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 30)
                    {
                        var slotTime = new TimeSpan(hour, minute, 0);
                        var slotDateTime = date.Date.Add(slotTime);

                        if (slotDateTime > minBookingTime)
                        {
                            timeSlots.Add(new TimeSlotDto
                            {
                                Time = slotTime,
                                DisplayTime = $"{hour:D2}:{minute:D2}",
                                IsAvailable = true
                            });
                        }
                    }
                }
            }
            return timeSlots;
        }



        public async Task<IEnumerable<AvailableDoctorDto>> GetAvailableDoctorsAsync(DateTime appointmentDate, string specialty)
        {
            var availableDoctors = await _doctorShiftRepository.GetAvailableDoctorsAsync(appointmentDate, specialty);
            return availableDoctors.Select(d => new AvailableDoctorDto
            {
                Id = d.Id,
                Name = d.Name,
                Specialty = d.Specialty,
                Email = d.Email
            }).ToList();
        }

        public async Task<IEnumerable<DoctorShiftDto>> GetDoctorScheduleAsync(int doctorId)
        {
            var shifts = await _doctorShiftRepository.GetByDoctorIdAsync(doctorId);
            var doctor = await _doctorRepository.GetByIdAsync(doctorId);

            if (doctor == null) return Enumerable.Empty<DoctorShiftDto>();

            return shifts.Select(s => new DoctorShiftDto
            {
                Id = s.Id,
                DoctorId = s.DoctorId,
                DoctorName = doctor.Name,
                DayOfWeek = s.DayOfWeek.ToString(),
                StartTime = s.StartTime.ToString(@"hh\:mm"),
                EndTime = s.EndTime.ToString(@"hh\:mm"),
                IsActive = s.IsActive
            }).ToList();
        }

        public async Task<IEnumerable<DateTime>> GetDoctorAvailableDatesAsync(int doctorId)
        {
            var activeShiftDays = (await _doctorShiftRepository.GetActiveShiftDaysAsync(doctorId)).ToHashSet();

            if (!activeShiftDays.Any()) return Enumerable.Empty<DateTime>();

            var availableDates = new List<DateTime>();
            var today = DateTime.Today;

            for (int i = 0; i < 30; i++)
            {
                var date = today.AddDays(i);
                if (activeShiftDays.Contains(date.DayOfWeek))
                {
                    availableDates.Add(date);
                }
            }
            return availableDates;
        }

        public async Task<IEnumerable<TimeSlotDto>> GetDoctorAvailableSlotsAsync(int doctorId, DateTime date, AppointmentType appointmentType)
        {
            var dayOfWeek = date.DayOfWeek;
            var shifts = await _doctorShiftRepository.GetShiftsByDoctorAndDayAsync(doctorId, dayOfWeek);

            if (shifts == null || !shifts.Any()) return Enumerable.Empty<TimeSlotDto>();

            // IMPORTANT: Fetch all appointments (InPerson and Online) to check for conflicts.
            var appointments = await _appointmentRepository.GetByDoctorIdAndDateAsync(doctorId, date);
            var bookedSlots = appointments
                .Where(a => a.Status != "Cancelled" && a.Status != "ExpiredPayment")
                .Select(a => TimeZoneInfo.ConvertTimeFromUtc(a.Date, VietnamZone).TimeOfDay)
                .ToHashSet();

            var availableSlots = new List<TimeSlotDto>();
            DateTime minBookingTime;
            if (appointmentType == AppointmentType.Online)
            {
                minBookingTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone).AddMinutes(15);
            }
            else // InPerson
            {
                minBookingTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone).AddHours(6);
            }

            foreach (var shift in shifts)
            {
                var currentTime = shift.StartTime;
                while (currentTime < shift.EndTime)
                {
                    var slotDateTime = date.Date.Add(currentTime);
                    // Standard slots are at :00 and :30
                    if (slotDateTime > minBookingTime && (currentTime.Minutes == 0 || currentTime.Minutes == 30) && !bookedSlots.Contains(currentTime))
                    {
                        availableSlots.Add(new TimeSlotDto
                        {
                            Time = currentTime,
                            DisplayTime = slotDateTime.ToString("HH:mm"),
                            IsAvailable = true
                        });
                    }
                    currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
                }
            }

            return availableSlots.OrderBy(s => s.Time);
        }

        public async Task<AppointmentResultDto> CreateOnlineAppointmentAsync(CreateOnlineAppointmentDto dto, int userId)
        {
            var user = await _authRepository.GetUserByIdAsync(userId);
            if (user == null || !user.PatientId.HasValue)
            {
                throw new InvalidOperationException("User is not linked to a patient profile.");
            }

            // Final validation to prevent race conditions
            var isSlotAvailable = await IsOnlineSlotAvailable(dto.DoctorId, dto.Date);
            if (!isSlotAvailable)
            {
                throw new InvalidOperationException("This time slot is no longer available.");
            }

            // The incoming time from the frontend is sent as an ISO string.
            // Convert it to UTC to ensure consistency, regardless of the server's local timezone.
            var incomingUtcTime = dto.Date.ToUniversalTime();

            // For validation, convert the UTC time to Vietnam time to check against local schedules.
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(incomingUtcTime, VietnamZone);

            var appointment = new Appointment
            {
                PatientId = user.PatientId.Value,
                DoctorId = dto.DoctorId,
                Date = incomingUtcTime, // Store time in UTC
                Type = AppointmentType.Online,
                Status = "PendingPayment",
                BookingFee = 200000, // 200k VND for online consultation
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdAppointment = await _appointmentRepository.CreateAsync(appointment);

            // Create a pending payment record immediately
            var payment = new Payment
            {
                PatientId = user.PatientId.Value,
                AppointmentId = createdAppointment.Id,
                PaymentType = PaymentTypes.BookingFee,
                Amount = createdAppointment.BookingFee,
                PaymentMethod = "Stripe", // Assuming Stripe is the default for online
                Status = PaymentStatuses.Pending,
                Description = $"Booking fee for online appointment on {createdAppointment.Date:yyyy-MM-dd HH:mm}",
                CreatedAt = DateTime.UtcNow
            };
            var createdPayment = await _paymentRepository.CreateAsync(payment);
            _logger.LogInformation("Created new Payment record {PaymentId} for Appointment {AppointmentId} immediately after booking.", createdPayment.Id, createdAppointment.Id);

            // Link the payment to the appointment
            createdAppointment.BookingPaymentId = createdPayment.Id;
            await _appointmentRepository.UpdateAsync(createdAppointment);

            _backgroundJobClient.Schedule<AppointmentApplicationService>(
                service => service.CheckAppointmentExpiration(createdAppointment.Id),
                TimeSpan.FromMinutes(30)
            );

            var doctor = await _doctorRepository.GetByIdAsync(createdAppointment.DoctorId);
            var patientUser = await _authRepository.GetUserByIdAsync(userId); // Fetch the user to get patient name
            
            await _rabbitMQService.PublishAppointmentCreatedAsync(
                new AppointmentCreatedEvent(
                    createdAppointment.Id,
                    createdAppointment.PatientId,
                    patientUser?.Patient?.Name ?? "Unknown", // Use patientUser to get patient name
                    createdAppointment.DoctorId,
                    doctor?.Name ?? "Unknown",
                    doctor?.Specialty ?? "Unknown",
                    createdAppointment.Date,
                    createdAppointment.Status,
                    createdAppointment.CreatedAt,
                    userId,
                    patientUser?.Role ?? "Unknown" // Use patientUser to get role
                )
            );

            return new AppointmentResultDto
            {
                Id = createdAppointment.Id,
                PatientId = createdAppointment.PatientId,
                DoctorId = createdAppointment.DoctorId,
                Date = createdAppointment.Date,
                Status = createdAppointment.Status,
                Type = createdAppointment.Type,
                BookingFee = createdAppointment.BookingFee,
                BookingPaymentId = createdAppointment.BookingPaymentId,
                PaymentExpiresAt = createdAppointment.PaymentExpiresAt,
                CreatedAt = createdAppointment.CreatedAt,
                DoctorName = doctor?.Name ?? "Unknown",
                PatientName = patientUser?.Patient?.Name ?? "Unknown" // Populate PatientName
            };
        }

        public async Task CancelAppointmentAsync(int appointmentId, int patientId, string cancelledByRole, int cancelledByUserId)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);

            if (appointment == null)
            {
                throw new KeyNotFoundException("Appointment not found.");
            }

            // Security check: ensure the user canceling is the one who booked it or an admin/doctor.
            if (cancelledByRole == "Patient" && appointment.PatientId != patientId)
            {
                throw new UnauthorizedAccessException("You are not authorized to cancel this appointment.");
            }

            // Check cancellation policy
            if (DateTime.UtcNow >= appointment.Date.AddMinutes(-15))
            {
                throw new InvalidOperationException("Cannot cancel an appointment less than 15 minutes before it starts.");
            }

            var originalStatus = appointment.Status;

            appointment.Status = "Cancelled";
            appointment.CancellationReason = $"Cancelled by {cancelledByRole}.";
            appointment.UpdatedAt = DateTime.UtcNow;

            await _appointmentRepository.UpdateAsync(appointment);
            _logger.LogInformation("Cancelled appointment {AppointmentId}", appointmentId);

            // Handle refund or payment status update
            if (originalStatus == "Scheduled" || originalStatus == "PendingPayment")
            {
                var payment = await _paymentRepository.GetByAppointmentIdAsync(appointmentId);
                if (payment != null)
                {
                    await HandleRefundAsync(payment, $"Appointment cancelled by {cancelledByRole}", cancelledByUserId, cancelledByRole);
                }
            }
        }

        public async Task CheckAppointmentExpiration(int appointmentId)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
            if (appointment != null && appointment.Status == "PendingPayment" && appointment.PaymentExpiresAt < DateTime.UtcNow)
            {
                appointment.Status = "ExpiredPayment";
                appointment.UpdatedAt = DateTime.UtcNow;
                await _appointmentRepository.UpdateAsync(appointment);
                _logger.LogInformation("Appointment {AppointmentId} status changed to ExpiredPayment.", appointmentId);

                var payment = await _paymentRepository.GetByAppointmentIdAsync(appointmentId);
                if (payment != null && payment.Status == PaymentStatuses.Pending)
                {
                    payment.Status = PaymentStatuses.Failed;
                    payment.FailureReason = "Payment window expired.";
                    await _paymentRepository.UpdateAsync(payment);
                    _logger.LogInformation("Payment {PaymentId} for appointment {AppointmentId} marked as Failed due to expiration.", payment.Id, appointmentId);
                }
            }
        }

        private async Task HandleRefundAsync(Payment payment, string reason, int cancelledByUserId, string cancelledByRole)
        {
            if (payment.Status == PaymentStatuses.Completed)
            {
                if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                {
                    _logger.LogWarning("Cannot refund payment {PaymentId} without a StripePaymentIntentId.", payment.Id);
                    return;
                }

                try
                {
                    var refund = await _stripePaymentService.RefundPaymentAsync(payment.StripePaymentIntentId, (long)payment.Amount, reason);

                    payment.Status = PaymentStatuses.Refunded;
                    payment.FailureReason = reason;
                    await _paymentRepository.UpdateAsync(payment);

                    _logger.LogInformation("Successfully refunded {Amount} for payment {PaymentId}. Stripe Refund ID: {RefundId}", payment.Amount, payment.Id, refund.Id);

                    await _rabbitMQService.PublishRefundProcessedAsync(new RefundProcessedEvent
                    {
                        BillingId = payment.Id,
                        AppointmentId = payment.AppointmentId ?? 0,
                        PatientId = payment.PatientId,
                        OriginalAmount = payment.Amount,
                        RefundAmount = payment.Amount,
                        PaymentMethod = payment.PaymentMethod,
                        OriginalTransactionId = payment.TransactionId,
                        RefundTransactionId = refund.Id,
                        RefundedAt = DateTime.UtcNow,
                        RefundedByUserId = cancelledByUserId,
                        RefundedByRole = cancelledByRole
                    });
                }
                catch (StripeException ex)
                {
                    _logger.LogError(ex, "Stripe error while refunding payment {PaymentId} for appointment {AppointmentId}", payment.Id, payment.AppointmentId);
                    // Optionally, set a specific app status for failed refunds
                }
            }
            else if (payment.Status == PaymentStatuses.Pending)
            {
                payment.Status = PaymentStatuses.Failed;
                payment.FailureReason = reason;
                await _paymentRepository.UpdateAsync(payment);
                _logger.LogInformation("Marked pending payment {PaymentId} as Failed for cancelled appointment {AppointmentId}", payment.Id, payment.AppointmentId);
            }
        }

        private async Task<bool> IsOnlineSlotAvailable(int doctorId, DateTime date)
        {
            // The incoming time from the frontend is sent as an ISO string.
            // Convert it to UTC to ensure consistency, regardless of the server's local timezone.
            var incomingUtcTime = date.ToUniversalTime();

            // For validation, convert the UTC time to Vietnam time to check against local schedules.
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(incomingUtcTime, VietnamZone);

            var availableSlots = await GetDoctorAvailableSlotsAsync(doctorId, vietnamTime.Date, AppointmentType.Online);
            return availableSlots.Any(s => s.Time == vietnamTime.TimeOfDay);
        }
    }

    public static class SpecificationExtensions
    {
        public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right)
        {
            return new AndSpecification<T>(left, right);
        }
    }

    public class AndSpecification<T> : BaseSpecification<T>
    {
        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
            : base(CombineExpressions(left.Criteria, right.Criteria))
        {
            left.Includes.ForEach(AddInclude);
            right.Includes.ForEach(AddInclude);
            left.IncludeStrings.ForEach(AddInclude);
            right.IncludeStrings.ForEach(AddInclude);
        }

        private static Expression<Func<T, bool>> CombineExpressions(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftVisitor = new ReplaceExpressionVisitor(left.Parameters[0], parameter);
            var leftBody = leftVisitor.Visit(left.Body);
            var rightVisitor = new ReplaceExpressionVisitor(right.Parameters[0], parameter);
            var rightBody = rightVisitor.Visit(right.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
        }

        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override Expression Visit(Expression? node) => node == _oldValue ? _newValue : base.Visit(node);
        }
    }
}

