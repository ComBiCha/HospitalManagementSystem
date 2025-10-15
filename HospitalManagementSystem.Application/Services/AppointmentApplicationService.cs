using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Application.DTOs.Appointment;
using HospitalManagementSystem.Application.DTOs.Common;
using HospitalManagementSystem.Domain.Specifications;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync, CountAsync
using System.Linq;

namespace HospitalManagementSystem.Application.Services
{
    public class AppointmentApplicationService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly IDoctorShiftRepository _doctorShiftRepository;
        private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        public AppointmentApplicationService(
            IAppointmentRepository appointmentRepository, 
            IDoctorRepository doctorRepository, 
            IDoctorShiftRepository doctorShiftRepository)
        {
            _appointmentRepository = appointmentRepository;
            _doctorRepository = doctorRepository;
            _doctorShiftRepository = doctorShiftRepository;
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

        public async Task<IEnumerable<TimeSlotDto>> GetDoctorAvailableSlotsAsync(int doctorId, DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;
            var shifts = await _doctorShiftRepository.GetShiftsByDoctorAndDayAsync(doctorId, dayOfWeek);

            if (shifts == null || !shifts.Any()) return Enumerable.Empty<TimeSlotDto>();

            var appointments = await _appointmentRepository.GetByDoctorIdAndDateAsync(doctorId, date);
            var bookedSlots = appointments.Select(a => TimeZoneInfo.ConvertTimeFromUtc(a.Date, VietnamZone).TimeOfDay).ToHashSet();

            var availableSlots = new List<TimeSlotDto>();
            var minBookingTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone).AddHours(6);

            foreach (var shift in shifts)
            {
                var currentTime = shift.StartTime;
                while (currentTime < shift.EndTime)
                {
                    var slotDateTime = date.Date.Add(currentTime);
                    if (slotDateTime > minBookingTime && !bookedSlots.Contains(currentTime))
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
