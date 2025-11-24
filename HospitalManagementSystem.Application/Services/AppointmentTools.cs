using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HospitalManagementSystem.Application.DTOs.AITools;
using HospitalManagementSystem.Domain.Repositories;
using System.Linq;
using HospitalManagementSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using HospitalManagementSystem.Domain.Payments;
using Hangfire;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace HospitalManagementSystem.Application.Services
{
    [McpServerToolType]
    public class AppointmentTools
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IDoctorShiftRepository _doctorShiftRepository;
        private readonly IStripePaymentService _stripePaymentService;
        private readonly IConfiguration _configuration;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<AppointmentTools> _logger;
        private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        public AppointmentTools(
            IDoctorRepository doctorRepository, 
            IPatientRepository patientRepository,
            IAppointmentRepository appointmentRepository,
            IPaymentRepository paymentRepository,
            IDoctorShiftRepository doctorShiftRepository,
            IStripePaymentService stripePaymentService,
            IConfiguration configuration,
            IBackgroundJobClient backgroundJobClient,
            ILogger<AppointmentTools> logger)
        {
            _doctorRepository = doctorRepository;
            _patientRepository = patientRepository;
            _appointmentRepository = appointmentRepository;
            _paymentRepository = paymentRepository;
            _doctorShiftRepository = doctorShiftRepository;
            _stripePaymentService = stripePaymentService;
            _configuration = configuration;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        [McpServerTool, Description("Books an appointment for a patient with a specific doctor at a given date and time. This is the final step and should only be called when all information is gathered. It returns a checkout URL for payment.")]
        public async Task<BookAppointmentResponseDTO> BookAppointment(BookAppointmentRequestDTO request)
        {
            // 1. VALIDATION
            var utcDate = request.DateTime.Kind == DateTimeKind.Utc 
                ? request.DateTime 
                : DateTime.SpecifyKind(request.DateTime, DateTimeKind.Utc);

            if (utcDate <= DateTime.UtcNow)
            {
                throw new ArgumentException("Appointment date must be in the future.");
            }

            var patient = await _patientRepository.GetPatientByIdAsync(request.PatientId);
            if (patient == null)
            {
                throw new KeyNotFoundException($"Patient with ID {request.PatientId} not found.");
            }

            var doctor = await _doctorRepository.GetByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new KeyNotFoundException($"Doctor with ID {request.DoctorId} not found.");
            }

            var hasConflict = await _appointmentRepository.HasConflictingAppointmentAsync(request.DoctorId, utcDate);
            if (hasConflict)
            {
                throw new InvalidOperationException("The selected time slot is no longer available.");
            }

            // 2. CREATE APPOINTMENT
            var appointment = new Appointment
            {
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                Date = utcDate,
                Status = "PendingPayment",
                Type = AppointmentType.InPerson, // Default to InPerson for this tool
                BookingFee = 50000, // Default fee
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdAppointment = await _appointmentRepository.CreateAsync(appointment);
            _logger.LogInformation("AI Tool: Created Appointment {AppointmentId}", createdAppointment.Id);

            // 3. SCHEDULE EXPIRATION JOB (Simplified)
            // This is a bit of a hack because we can't easily access the service provider here.
            // A better solution would be to use a proper message queue if this becomes complex.
            _backgroundJobClient.Schedule(
                () => new AppointmentApplicationService(_appointmentRepository, _doctorRepository, _doctorShiftRepository, null, _backgroundJobClient, null, _paymentRepository, _stripePaymentService, null).CheckAppointmentExpiration(createdAppointment.Id),
                TimeSpan.FromMinutes(30)
            );

            // 4. CREATE PAYMENT RECORD
            var payment = new Payment
            {
                PatientId = createdAppointment.PatientId,
                AppointmentId = createdAppointment.Id,
                PaymentType = PaymentTypes.BookingFee,
                Amount = createdAppointment.BookingFee,
                PaymentMethod = "Stripe",
                Status = PaymentStatuses.Pending,
                Description = $"Booking fee for appointment on {createdAppointment.Date:yyyy-MM-dd HH:mm}",
                CreatedAt = DateTime.UtcNow
            };
            var createdPayment = await _paymentRepository.CreateAsync(payment);
            _logger.LogInformation("AI Tool: Created Payment {PaymentId} for Appointment {AppointmentId}", createdPayment.Id, createdAppointment.Id);

            // 5. CREATE STRIPE CHECKOUT SESSION
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:3000";
            var metadata = new Dictionary<string, string>
            {
                { "payment_id", createdPayment.Id.ToString() },
                { "appointment_id", createdAppointment.Id.ToString() },
                { "patient_id", createdAppointment.PatientId.ToString() },
                { "payment_type", PaymentTypes.BookingFee }
            };

            var session = await _stripePaymentService.CreateCheckoutSessionAsync(
                (long)createdPayment.Amount,
                "Phí đặt lịch khám",
                payment.Description,
                patient.Email, // Use patient's email
                $"{baseUrl}/api/payments/success?payment_id={createdPayment.Id}",
                $"{baseUrl}/api/payments/cancel?payment_id={createdAppointment.Id}",
                metadata
            );

            createdPayment.StripeSessionId = session.Id;
            await _paymentRepository.UpdateAsync(createdPayment);
            _logger.LogInformation("AI Tool: Created Stripe Session {SessionId} for Payment {PaymentId}", session.Id, createdPayment.Id);

            // 6. RETURN RESPONSE
            return new BookAppointmentResponseDTO
            {
                AppointmentId = createdAppointment.Id,
                Status = createdAppointment.Status,
                CheckoutUrl = session.Url
            };
        }

        [McpServerTool, Description("Gets a list of available dates for a specific doctor. Returns dates in yyyy-MM-dd format.")]
        public async Task<IEnumerable<string>> GetAvailableDatesForDoctor(int doctorId)
        {
            var activeShiftDays = (await _doctorShiftRepository.GetActiveShiftDaysAsync(doctorId)).ToHashSet();

            if (!activeShiftDays.Any()) return Enumerable.Empty<string>();

            var availableDates = new List<string>();
            var today = DateTime.Today;

            for (int i = 0; i < 30; i++)
            {
                var date = today.AddDays(i);
                if (activeShiftDays.Contains(date.DayOfWeek))
                {
                    availableDates.Add(date.ToString("yyyy-MM-dd"));
                }
            }
            return availableDates;
        }

        [McpServerTool, Description("Gets a list of available time slots for a specific doctor on a given date. Date must be in yyyy-MM-dd format. Returns times in HH:mm format.")]
        public async Task<IEnumerable<string>> GetAvailableSlotsForDoctor(int doctorId, string date)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                throw new ArgumentException("Invalid date format. Please use yyyy-MM-dd.");
            }

            var dayOfWeek = parsedDate.DayOfWeek;
            var shifts = await _doctorShiftRepository.GetShiftsByDoctorAndDayAsync(doctorId, dayOfWeek);

            if (shifts == null || !shifts.Any()) return Enumerable.Empty<string>();

            var appointments = await _appointmentRepository.GetByDoctorIdAndDateAsync(doctorId, parsedDate);
            var bookedSlots = appointments
                .Where(a => a.Status != "Cancelled" && a.Status != "ExpiredPayment")
                .Select(a => TimeZoneInfo.ConvertTimeFromUtc(a.Date, VietnamZone).TimeOfDay)
                .ToHashSet();

            var availableSlots = new List<string>();
            // Assuming InPerson appointment type for 6 hour rule
            var minBookingTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone).AddHours(6);

            foreach (var shift in shifts)
            {
                var currentTime = shift.StartTime;
                while (currentTime < shift.EndTime)
                {
                    var slotDateTime = parsedDate.Date.Add(currentTime);
                    if (slotDateTime > minBookingTime && !bookedSlots.Contains(currentTime))
                    {
                        availableSlots.Add(slotDateTime.ToString("HH:mm"));
                    }
                    currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
                }
            }

            return availableSlots.OrderBy(s => s);
        }

        [McpServerTool, Description("Gets a list of doctors. Can be filtered by specialty.")]
        public async Task<IEnumerable<DoctorInfoDTO>> GetDoctorsBySpecialty(string? specialty = null)
        {
            var doctors = await _doctorRepository.GetAllAsync();
            if (!string.IsNullOrWhiteSpace(specialty))
            {
                doctors = doctors.Where(d => d.Specialty.Equals(specialty, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return doctors.Select(d => new DoctorInfoDTO
            {
                Id = d.Id,
                Name = d.Name,
                Specialty = d.Specialty
            });
        }

        [McpServerTool, Description("Gets a list of all available medical specialties.")]
        public async Task<IEnumerable<string>> GetSpecialties()
        {
            var doctors = await _doctorRepository.GetAllAsync();
            return doctors.Select(d => d.Specialty).Distinct().ToList();
        }
    }
}
