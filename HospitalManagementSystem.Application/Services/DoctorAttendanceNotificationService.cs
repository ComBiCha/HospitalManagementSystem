using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace HospitalManagementSystem.Application.Services;

public class DoctorAttendanceNotificationService
{
    private readonly IDoctorShiftRepository _shiftRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorAttendanceRepository _attendanceRepository;
    private readonly ILogger<DoctorAttendanceNotificationService> _logger;

    public DoctorAttendanceNotificationService(
        IDoctorShiftRepository shiftRepository,
        INotificationRepository notificationRepository,
        IDoctorRepository doctorRepository,
        IAppointmentRepository appointmentRepository,
        IDoctorAttendanceRepository attendanceRepository,
        ILogger<DoctorAttendanceNotificationService> logger)
    {
        _shiftRepository = shiftRepository;
        _notificationRepository = notificationRepository;
        _doctorRepository = doctorRepository;
        _appointmentRepository = appointmentRepository;
        _attendanceRepository = attendanceRepository;
        _logger = logger;
    }

    // Schedule check-in notifications for all doctors
    public async Task ScheduleCheckInNotificationsAsync()
    {
        // Use VN timezone explicitly
        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var nowUtc = DateTime.UtcNow;
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
        var today = nowVn.Date;
        var currentDayOfWeek = (int)nowVn.DayOfWeek;

        var allDoctors = await _doctorRepository.GetAllAsync();

        _logger.LogInformation("Starting to schedule notifications for {Count} doctors on day {Day} at {Time} VN time", 
            allDoctors.Count(), currentDayOfWeek, nowVn);

        foreach (var doctor in allDoctors)
        {
            var shifts = await _shiftRepository.GetDoctorShiftsAsync(doctor.Id);
            var todayShifts = shifts.Where(s =>
                s.IsActive && (int)s.DayOfWeek == currentDayOfWeek).ToList();

            foreach (var shift in todayShifts)
            {
                var shiftStart = today.Add(shift.StartTime);
                var shiftEnd = today.Add(shift.EndTime);
                var notificationTime = shiftStart.AddMinutes(-10);

                // Schedule check-in notification
                if (notificationTime > nowVn)
                {
                    var delay = notificationTime - nowVn;
                    BackgroundJob.Schedule(
                        () => SendCheckInNotificationAsync(doctor.Id, shift.Id),
                        delay
                    );
                    _logger.LogInformation("Scheduled check-in notification for doctor {DoctorId} at {Time} VN time (delay: {Delay})", 
                        doctor.Id, notificationTime, delay);
                }
                else
                {
                    _logger.LogInformation("Skipped check-in notification for doctor {DoctorId} - time passed ({NotificationTime} < {Now})", 
                        doctor.Id, notificationTime, nowVn);
                }
                
                // Schedule checkout check right after shift ends
                if (shiftEnd > nowVn)
                {
                    var checkoutDelay = shiftEnd - nowVn;
                    BackgroundJob.Schedule(
                        () => CheckAndNotifyCheckoutAsync(doctor.Id),
                        checkoutDelay
                    );
                    
                    _logger.LogInformation("Scheduled checkout check for doctor {DoctorId} at shift end {ShiftEnd} VN time (delay: {Delay})", 
                        doctor.Id, shiftEnd, checkoutDelay);
                }
                else
                {
                    _logger.LogInformation("Skipped checkout check for doctor {DoctorId} - shift ended ({ShiftEnd} < {Now})", 
                        doctor.Id, shiftEnd, nowVn);
                }
            }
        }
    }
    
    // Initialize recurring job to schedule daily notifications
    public void InitializeRecurringJobs()
    {
        // Run at 7:00 AM VN time (before morning shift 8:00-11:00)
        RecurringJob.AddOrUpdate(
            "schedule-morning-notifications",
            () => ScheduleCheckInNotificationsAsync(),
            "0 7 * * *", // 07:00 every day (VN timezone)
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            }
        );
        
        // Run at 12:00 PM VN time (before afternoon shift 13:00-17:00)
        RecurringJob.AddOrUpdate(
            "schedule-afternoon-notifications",
            () => ScheduleCheckInNotificationsAsync(),
            "0 12 * * *", // 12:00 every day (VN timezone)
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            }
        );
        
        _logger.LogInformation("Initialized recurring jobs: 7:00 AM and 12:00 PM VN time");
    }

    // Send check-in notification
    public async Task SendCheckInNotificationAsync(int doctorId, int shiftId)
    {
        try
        {
            var doctor = await _doctorRepository.GetByIdAsync(doctorId);
            var shift = await _shiftRepository.GetByIdAsync(shiftId);

            if (doctor == null || shift == null) return;

            // Check if already checked in
            var attendance = await _attendanceRepository.GetActiveAttendanceAsync(doctorId);
            if (attendance != null) return;

            // Get UserId from DoctorId
            var userId = await _notificationRepository.GetUserIdFromDoctorIdAsync(doctorId);
            if (userId == null)
            {
                _logger.LogWarning("Cannot find UserId for DoctorId: {DoctorId}", doctorId);
                return;
            }

            var nowUtc = DateTime.UtcNow;
            
            // Create notification
            var notification = new Notification
            {
                UserId = userId.Value,
                Recipient = doctor.Email ?? "",
                Subject = "⏰ Đã đến giờ check-in",
                Content = $"Ca làm việc từ {shift.StartTime:hh\\:mm} đến {shift.EndTime:hh\\:mm}. Bạn có thể check-in ngay bây giờ!",
                ChannelType = NotificationChannels.Push,
                Status = NotificationStatus.Sent,
                IsRead = false,
                CreatedAt = nowUtc,
                SentAt = nowUtc,
                Metadata = $"{{\"shiftId\":{shiftId},\"action\":\"check-in\",\"type\":\"CheckIn\"}}"
            };

            await _notificationRepository.CreateAsync(notification);
            _logger.LogInformation("Check-in notification sent to doctor {DoctorId} for shift {ShiftId}", doctorId, shiftId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending check-in notification");
        }
    }

    // Check if doctor can checkout
    public async Task CheckAndNotifyCheckoutAsync(int doctorId)
    {
        try
        {
            _logger.LogInformation("========== CheckAndNotifyCheckoutAsync START for doctor {DoctorId} ==========", doctorId);
            
            var attendance = await _attendanceRepository.GetActiveAttendanceAsync(doctorId);
            if (attendance == null) 
            {
                _logger.LogWarning("No active attendance found for doctor {DoctorId}", doctorId);
                return;
            }
            
            _logger.LogInformation("Found active attendance: Id={AttendanceId}, ShiftId={ShiftId}, CheckInTime={CheckInTime}, CheckOutTime={CheckOutTime}", 
                attendance.Id, attendance.ShiftId, attendance.CheckInTime, attendance.CheckOutTime);
            
            if (attendance.Shift == null) 
            {
                _logger.LogWarning("Shift is null for attendance {AttendanceId}, trying to load...", attendance.Id);
                var shift = await _shiftRepository.GetByIdAsync(attendance.ShiftId);
                if (shift == null)
                {
                    _logger.LogError("Cannot load shift {ShiftId} for attendance {AttendanceId}", attendance.ShiftId, attendance.Id);
                    return;
                }
                attendance.Shift = shift;
                _logger.LogInformation("Loaded shift: Id={ShiftId}, StartTime={Start}, EndTime={End}", 
                    shift.Id, shift.StartTime, shift.EndTime);
            }

            var nowUtc = DateTime.UtcNow;
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var today = nowVn.Date;
            var shiftEnd = today.Add(attendance.Shift.EndTime);

            _logger.LogInformation("Time check: NowVN={Now}, ShiftEnd={ShiftEnd}, ShiftEnded={Ended}", 
                nowVn, shiftEnd, nowVn >= shiftEnd);

            // Only check if shift has ended
            if (nowVn < shiftEnd) 
            {
                _logger.LogInformation("Shift not ended yet for doctor {DoctorId}, exiting", doctorId);
                return;
            }

            // Get all appointments for this shift
            _logger.LogInformation("Fetching appointments for doctor {DoctorId} on {Date}", doctorId, today);
            
            // Convert to UTC for database query
            var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
            var tomorrowUtc = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
            
            var appointments = await _appointmentRepository.GetDoctorAppointmentsAsync(
                doctorId,
                todayUtc,
                tomorrowUtc
            );

            _logger.LogInformation("Total appointments fetched: {Count}", appointments.Count());

            var shiftAppointments = appointments.Where(a =>
            {
                var apptTime = a.Date.TimeOfDay;
                var inShift = apptTime >= attendance.Shift.StartTime && apptTime <= attendance.Shift.EndTime;
                _logger.LogInformation("Appointment {Id}: Time={Time}, Status={Status}, InShift={InShift}", 
                    a.Id, apptTime, a.Status, inShift);
                return inShift;
            }).ToList();

            _logger.LogInformation("Found {Count} appointments in shift for doctor {DoctorId}", 
                shiftAppointments.Count, doctorId);

            // Exclude Cancelled and ExpiredPayment appointments from the check
            var validAppointments = shiftAppointments
                .Where(a => a.Status != "Cancelled" && a.Status != "ExpiredPayment")
                .ToList();

            _logger.LogInformation("Valid appointments (excluding Cancelled/ExpiredPayment): {Count}", validAppointments.Count);
            
            foreach (var appt in validAppointments)
            {
                _logger.LogInformation("  - Appointment {Id}: Status={Status}, Date={Date}", 
                    appt.Id, appt.Status, appt.Date);
            }

            // Check if all valid appointments are completed or hospitalized
            var incompleteAppointments = validAppointments
                .Where(a => a.Status != "Completed" && a.Status != "Hospitalized")
                .ToList();
            
            bool allCompleted = validAppointments.Count == 0 || incompleteAppointments.Count == 0;

            _logger.LogInformation("Incomplete appointments: {Count}, AllCompleted: {AllCompleted}", 
                incompleteAppointments.Count, allCompleted);
            
            foreach (var appt in incompleteAppointments)
            {
                _logger.LogWarning("  - Incomplete appointment {Id}: Status={Status}", appt.Id, appt.Status);
            }

            if (allCompleted)
            {
                _logger.LogInformation("All appointments completed! Creating checkout notification...");
                
                var doctor = await _doctorRepository.GetByIdAsync(doctorId);
                if (doctor == null)
                {
                    _logger.LogError("Cannot find doctor {DoctorId}", doctorId);
                    return;
                }
                
                _logger.LogInformation("Doctor found: Id={DoctorId}, Email={Email}", doctor.Id, doctor.Email);
                
                // Get UserId from DoctorId
                var userId = await _notificationRepository.GetUserIdFromDoctorIdAsync(doctorId);
                if (userId == null)
                {
                    _logger.LogError("Cannot find UserId for DoctorId: {DoctorId}", doctorId);
                    return;
                }
                
                _logger.LogInformation("UserId found: {UserId}", userId.Value);
                
                // Create checkout notification
                var notification = new Notification
                {
                    UserId = userId.Value,
                    Recipient = doctor.Email ?? "",
                    Subject = "✅ Đã hoàn thành ca làm việc",
                    Content = $"Tất cả bệnh nhân đã được khám xong. Bạn có thể check-out ngay!",
                    ChannelType = NotificationChannels.Push,
                    Status = NotificationStatus.Sent,
                    IsRead = false,
                    CreatedAt = nowUtc,
                    SentAt = nowUtc,
                    Metadata = $"{{\"attendanceId\":{attendance.Id},\"action\":\"check-out\",\"type\":\"CheckOut\"}}"
                };

                _logger.LogInformation("Creating notification: UserId={UserId}, Subject={Subject}, Content={Content}", 
                    notification.UserId, notification.Subject, notification.Content);

                var createdNotification = await _notificationRepository.CreateAsync(notification);
                
                _logger.LogInformation("✅ Notification created successfully! Id={NotificationId} for doctor {DoctorId}", 
                    createdNotification.Id, doctorId);
            }
            else
            {
                _logger.LogWarning("Cannot send checkout notification - {Count} appointments still incomplete", 
                    incompleteAppointments.Count);
            }
            
            _logger.LogInformation("========== CheckAndNotifyCheckoutAsync END for doctor {DoctorId} ==========", doctorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in CheckAndNotifyCheckoutAsync for doctor {DoctorId}", doctorId);
        }
    }

    // Schedule checkout check when appointment status changes
    public async Task OnAppointmentStatusChangedAsync(int appointmentId)
    {
        try
        {
            var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null) return;

            if (appointment.Status == "Completed" || appointment.Status == "Hospitalized")
            {
                await CheckAndNotifyCheckoutAsync(appointment.DoctorId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling appointment status change");
        }
    }
}
