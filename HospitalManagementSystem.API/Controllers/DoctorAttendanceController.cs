using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Doctor")]
public class DoctorAttendanceController : ControllerBase
{
    private readonly IDoctorAttendanceRepository _attendanceRepository;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IDoctorShiftRepository _shiftRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ILogger<DoctorAttendanceController> _logger;

    public DoctorAttendanceController(
        IDoctorAttendanceRepository attendanceRepository,
        IDoctorRepository doctorRepository,
        IDoctorShiftRepository shiftRepository,
        IAppointmentRepository appointmentRepository,
        ILogger<DoctorAttendanceController> logger)
    {
        _attendanceRepository = attendanceRepository;
        _doctorRepository = doctorRepository;
        _shiftRepository = shiftRepository;
        _appointmentRepository = appointmentRepository;
        _logger = logger;
    }

    [HttpGet("check-in-status")]
    public async Task<IActionResult> GetCheckInStatus()
    {
        try
        {
            var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
            if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
            {
                return BadRequest(new { message = "Doctor ID not found in token" });
            }

            var now = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, 
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time") 
            );
            var today = now.Date;
            var currentDayOfWeek = (int)now.DayOfWeek; // 0=Sunday, 1=Monday, ..., 6=Saturday

            // Get doctor's shifts for today
            var todayShifts = await _shiftRepository.GetDoctorShiftsAsync(doctorId);
            var activeShifts = todayShifts
                .Where(s => s.IsActive && (int)s.DayOfWeek == currentDayOfWeek)
                .OrderBy(s => s.StartTime)
                .ToList();

            if (!activeShifts.Any())
            {
                return Ok(new 
                { 
                    canCheckIn = false, 
                    message = "Không có ca làm việc hôm nay",
                    isCheckedIn = false
                });
            }

            // Find current or next shift
            DoctorShift? activeShift = null;
            
            _logger.LogInformation("Checking shifts for DoctorId: {DoctorId}, Now: {Now}, Today: {Today}", 
                doctorId, now, today);
            
            // First, try to find shift currently in progress
            foreach (var shift in activeShifts)
            {
                var shiftStart = today.Add(shift.StartTime);
                var shiftEnd = today.Add(shift.EndTime);
                
                _logger.LogInformation("Checking shift {ShiftId}: Start={Start}, End={End}, Now={Now}, InProgress={InProgress}", 
                    shift.Id, shiftStart, shiftEnd, now, now >= shiftStart && now <= shiftEnd);
                
                // Check if we're currently IN the shift time (not check-in window)
                if (now >= shiftStart && now <= shiftEnd)
                {
                    activeShift = shift;
                    _logger.LogInformation("Found active shift: {ShiftId}, Start: {Start}, End: {End}, Now: {Now}", 
                        shift.Id, shiftStart, shiftEnd, now);
                    break;
                }
            }
            
            // If not in any shift, check for upcoming shift with check-in window
            if (activeShift == null)
            {
                foreach (var shift in activeShifts)
                {
                    var shiftStart = today.Add(shift.StartTime);
                    var checkInWindow = shiftStart.AddMinutes(-10);
                    
                    // Check if we're in check-in window but shift hasn't started
                    if (now >= checkInWindow && now < shiftStart)
                    {
                        activeShift = shift;
                        _logger.LogInformation("Found shift in check-in window: {ShiftId}", shift.Id);
                        break;
                    }
                }
            }
            
            // If still no shift, find next upcoming shift
            if (activeShift == null)
            {
                activeShift = activeShifts.FirstOrDefault(s =>
                {
                    var shiftStart = today.Add(s.StartTime);
                    return now < shiftStart.AddMinutes(-10);
                });
                if (activeShift != null)
                {
                    _logger.LogInformation("Found next upcoming shift: {ShiftId}", activeShift.Id);
                }
            }

            // If all shifts passed, take the last one
            if (activeShift == null)
            {
                activeShift = activeShifts.Last();
                _logger.LogInformation("All shifts passed, using last shift: {ShiftId}", activeShift.Id);
            }

            // Check if already checked in
            var activeAttendance = await _attendanceRepository.GetActiveAttendanceAsync(doctorId);
            if (activeAttendance != null)
            {
                // Load shift info if not loaded
                if (activeAttendance.Shift == null)
                {
                    activeAttendance.Shift = await _shiftRepository.GetByIdAsync(activeAttendance.ShiftId);
                }
                
                // Check if can checkout (shift ended + status is Active+OffDuty)
                var doctor = await _doctorRepository.GetByIdAsync(doctorId);
                var shiftEnd = activeAttendance.Shift != null ? today.Add(activeAttendance.Shift.EndTime) : now;
                var canCheckOut = now >= shiftEnd && 
                                  doctor != null && 
                                  doctor.Status.HasFlag(DoctorStatus.Active) && 
                                  doctor.Status.HasFlag(DoctorStatus.OffDuty);
                
                // Convert UTC times to VN timezone for response
                var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                
                return Ok(new
                {
                    canCheckIn = false,
                    isCheckedIn = true,
                    canCheckOut = canCheckOut,
                    message = "Đã check-in",
                    attendance = new 
                    {
                        activeAttendance.Id,
                        activeAttendance.DoctorId,
                        activeAttendance.ShiftId,
                        activeAttendance.ShiftDate,
                        // Return UTC time - let frontend handle timezone display
                        CheckInTime = activeAttendance.CheckInTime,
                        CheckOutTime = activeAttendance.CheckOutTime,
                        activeAttendance.Status,
                        activeAttendance.CheckInNote,
                        activeAttendance.CheckOutNote,
                        activeAttendance.Shift
                    },
                    shift = activeShift,
                    allShifts = activeShifts
                });
            }

            // Check if can check-in (10 minutes before shift start until shift ends)
            var shiftStartDateTime = today.Add(activeShift.StartTime);
            var shiftEndDateTime = today.Add(activeShift.EndTime);
            var earliestCheckIn = shiftStartDateTime.AddMinutes(-10);
            var canCheckIn = now >= earliestCheckIn && now <= shiftEndDateTime; // Can check-in until shift ends

            // Check if this specific shift already has completed attendance (checked in and out)
            var todayUtcCheck = DateTime.SpecifyKind(today, DateTimeKind.Utc);
            var todayAttendance = await _attendanceRepository.GetTodayAttendanceAsync(doctorId, todayUtcCheck);
            
            // Only show "completed" if the attendance is for THIS shift AND is checked out
            if (todayAttendance != null && 
                todayAttendance.ShiftId == activeShift.Id && 
                todayAttendance.CheckOutTime != null)
            {
                // This specific shift already completed
                return Ok(new
                {
                    canCheckIn = false,
                    isCheckedIn = false,
                    message = "Đã hoàn thành ca làm việc này",
                    shift = activeShift,
                    attendance = todayAttendance,
                    allShifts = activeShifts,
                    isCompleted = true
                });
            }

            return Ok(new
            {
                canCheckIn,
                isCheckedIn = false,
                message = canCheckIn 
                    ? "Có thể check-in" 
                    : $"Chỉ có thể check-in từ {earliestCheckIn:HH:mm} đến {shiftEndDateTime:HH:mm}",
                shift = activeShift,
                earliestCheckInTime = earliestCheckIn,
                shiftStartTime = shiftStartDateTime,
                allShifts = activeShifts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking check-in status");
            return StatusCode(500, new { message = "Lỗi kiểm tra trạng thái check-in" });
        }
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        try
        {
            var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
            if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
            {
                return BadRequest(new { message = "Doctor ID not found in token" });
            }

            var nowUtc = DateTime.UtcNow;
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var today = nowVn.Date;
            var currentDayOfWeek = (int)nowVn.DayOfWeek;

            _logger.LogInformation("Check-in attempt: DoctorId={DoctorId}, Now={Now}, DayOfWeek={DayOfWeek}", 
                doctorId, nowVn, currentDayOfWeek);

            // Get today's shifts
            var todayShifts = await _shiftRepository.GetDoctorShiftsAsync(doctorId);
            
            _logger.LogInformation("Found {Count} total shifts for doctor", todayShifts.Count());
            
            var activeShifts = todayShifts
                .Where(s => s.IsActive && (int)s.DayOfWeek == currentDayOfWeek)
                .OrderBy(s => s.StartTime)
                .ToList();

            if (!activeShifts.Any())
            {
                _logger.LogWarning("No active shift found for DayOfWeek={DayOfWeek}", currentDayOfWeek);
                return BadRequest(new { message = "Không tìm thấy ca làm việc hôm nay" });
            }

            // Find current shift using same logic as GET check-in-status
            DoctorShift? activeShift = null;
            
            // First, try to find shift currently in progress
            foreach (var shift in activeShifts)
            {
                var checkStart = today.Add(shift.StartTime);
                var checkEnd = today.Add(shift.EndTime);
                
                if (nowVn >= checkStart && nowVn <= checkEnd)
                {
                    activeShift = shift;
                    break;
                }
            }
            
            // If not in any shift, check for upcoming shift with check-in window
            if (activeShift == null)
            {
                foreach (var shift in activeShifts)
                {
                    var checkStart = today.Add(shift.StartTime);
                    var checkInWindow = checkStart.AddMinutes(-10);
                    
                    if (nowVn >= checkInWindow && nowVn < checkStart)
                    {
                        activeShift = shift;
                        break;
                    }
                }
            }

            if (activeShift == null)
            {
                _logger.LogWarning("No valid shift found for check-in at {Now}", nowVn);
                return BadRequest(new { message = "Không có ca làm việc phù hợp để check-in" });
            }

            _logger.LogInformation("Found shift: Id={ShiftId}, Start={Start}, End={End}", 
                activeShift.Id, activeShift.StartTime, activeShift.EndTime);

            // Validate check-in time - allow check-in until shift ends
            var shiftStart = today.Add(activeShift.StartTime);
            var shiftEnd = today.Add(activeShift.EndTime);
            var earliestCheckIn = shiftStart.AddMinutes(-10);
            var latestCheckIn = shiftEnd; // Can check-in until shift ends
            
            _logger.LogInformation("Validation: Now={Now}, EarliestCheckIn={Earliest}, LatestCheckIn={Latest}", 
                nowVn, earliestCheckIn, latestCheckIn);
            
            if (nowVn < earliestCheckIn || nowVn > latestCheckIn)
            {
                _logger.LogWarning("Check-in time validation failed");
                return BadRequest(new 
                { 
                    message = $"Chỉ có thể check-in từ {earliestCheckIn:HH:mm} đến {latestCheckIn:HH:mm}" 
                });
            }

            // Check if already checked in today (and not checked out yet)
            var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
            var existingAttendance = await _attendanceRepository.GetTodayAttendanceAsync(doctorId, todayUtc);
            
            _logger.LogInformation("Existing attendance check: Found={Found}, ShiftId={ShiftId}, CheckInTime={CheckIn}, CheckOutTime={CheckOut}", 
                existingAttendance != null, existingAttendance?.ShiftId, existingAttendance?.CheckInTime, existingAttendance?.CheckOutTime);
            
            // Case 1: There's an active check-in (not checked out) - can't check-in again
            if (existingAttendance != null && existingAttendance.CheckInTime != null && existingAttendance.CheckOutTime == null)
            {
                _logger.LogWarning("Doctor {DoctorId} already checked in - CheckInTime: {CheckInTime}, CheckOutTime: {CheckOutTime}", 
                    doctorId, existingAttendance.CheckInTime, existingAttendance.CheckOutTime);
                return BadRequest(new { message = "Đã check-in rồi!" });
            }
            
            // Case 2: There's a completed attendance - check if it's for THIS shift or a different shift
            if (existingAttendance != null && existingAttendance.CheckOutTime != null)
            {
                if (existingAttendance.ShiftId == activeShift.Id)
                {
                    // Same shift - already completed this shift
                    _logger.LogWarning("Doctor {DoctorId} already completed this shift {ShiftId}", doctorId, activeShift.Id);
                    return BadRequest(new { message = "Đã hoàn thành ca làm việc này rồi!" });
                }
                else
                {
                    // Different shift - this is OK, we'll create a new attendance below
                    _logger.LogInformation("Found completed attendance for different shift (ShiftId={OldShiftId}), allowing new check-in for ShiftId={NewShiftId}", 
                        existingAttendance.ShiftId, activeShift.Id);
                    existingAttendance = null; // Don't reuse the old attendance
                }
            }

            // Create or update attendance record
            DoctorAttendance attendance;
            if (existingAttendance != null && existingAttendance.CheckOutTime == null)
            {
                // Reuse existing attendance that hasn't been checked out
                existingAttendance.ShiftId = activeShift.Id; // Update to current shift
                existingAttendance.CheckInTime = nowUtc;
                existingAttendance.Status = "CheckedIn";
                existingAttendance.CheckInNote = request.Note;
                existingAttendance.UpdatedAt = nowUtc;
                attendance = await _attendanceRepository.UpdateAsync(existingAttendance);
            }
            else
            {
                // Create new attendance
                var todayUtcDb = DateTime.SpecifyKind(today, DateTimeKind.Utc);
                
                _logger.LogInformation("Creating attendance: nowUtc.Kind={NowKind}, todayUtcDb.Kind={TodayKind}, CheckInTime={CheckIn}, ShiftDate={ShiftDate}", 
                    nowUtc.Kind, todayUtcDb.Kind, nowUtc, todayUtcDb);
                
                attendance = new DoctorAttendance
                {
                    DoctorId = doctorId,
                    ShiftId = activeShift.Id,
                    ShiftDate = todayUtcDb,
                    CheckInTime = nowUtc,
                    Status = "CheckedIn",
                    CheckInNote = request.Note,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                
                _logger.LogInformation("Attendance before save - ShiftDate.Kind={Kind1}, CheckInTime.Kind={Kind2}, CreatedAt.Kind={Kind3}, UpdatedAt.Kind={Kind4}", 
                    attendance.ShiftDate.Kind, 
                    attendance.CheckInTime.HasValue ? attendance.CheckInTime.Value.Kind.ToString() : "null", 
                    attendance.CreatedAt.Kind,
                    attendance.UpdatedAt.HasValue ? attendance.UpdatedAt.Value.Kind.ToString() : "null");
                
                attendance = await _attendanceRepository.CreateAsync(attendance);
            }
            
            // Update doctor status to Active + OffDuty (at hospital but not examining)
            var doctor = await _doctorRepository.GetByIdAsync(doctorId);
            if (doctor != null)
            {
                doctor.Status = DoctorStatus.Active | DoctorStatus.OffDuty;
                doctor.UpdatedAt = nowUtc;
                await _doctorRepository.UpdateAsync(doctor);
            }

            _logger.LogInformation("Doctor {DoctorId} checked in at {Time}", doctorId, nowVn);

            return Ok(new
            {
                message = "Check-in thành công!",
                attendance,
                checkInTime = nowVn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return StatusCode(500, new { message = "Lỗi khi check-in" });
        }
    }


    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request)
    {
        try
        {
            var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
            if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
            {
                return BadRequest(new { message = "Doctor ID not found in token" });
            }

            var nowUtc = DateTime.UtcNow;
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var today = nowVn.Date;

            // Get doctor and check status
            var doctor = await _doctorRepository.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                return BadRequest(new { message = "Không tìm thấy bác sĩ!" });
            }

            // Check if doctor is Active + OffDuty 
            if (!doctor.Status.HasFlag(DoctorStatus.Active) || !doctor.Status.HasFlag(DoctorStatus.OffDuty))
            {
                return BadRequest(new { message = "Chỉ có thể check-out khi đang ở trạng thái sẵn sàng (không khám bệnh)!" });
            }

            // Get active attendance
            var activeAttendance = await _attendanceRepository.GetActiveAttendanceAsync(doctorId);
            if (activeAttendance == null)
            {
                return BadRequest(new { message = "Chưa check-in!" });
            }

            if (activeAttendance.CheckOutTime != null)
            {
                return BadRequest(new { message = "Đã check-out rồi!" });
            }

            // Load shift information
            if (activeAttendance.Shift == null)
            {
                var shift = await _shiftRepository.GetByIdAsync(activeAttendance.ShiftId);
                activeAttendance.Shift = shift;
            }

            if (activeAttendance.Shift == null)
            {
                return BadRequest(new { message = "Không tìm thấy thông tin ca làm việc!" });
            }

            // Check if shift has ended
            var shiftEnd = today.Add(activeAttendance.Shift.EndTime);
            if (nowVn < shiftEnd)
            {
                return BadRequest(new { 
                    message = $"Chỉ có thể check-out sau khi ca làm việc kết thúc ({shiftEnd:HH:mm})!" 
                });
            }

            // Check if all appointments in shift are completed (excluding Cancelled/ExpiredPayment)
            // Convert to UTC for database query
            var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
            var tomorrowUtc = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
            
            var appointments = await _appointmentRepository.GetDoctorAppointmentsAsync(
                doctorId,
                todayUtc,
                tomorrowUtc
            );

            var shiftAppointments = appointments.Where(a =>
            {
                var apptTime = a.Date.TimeOfDay;
                return apptTime >= activeAttendance.Shift.StartTime &&
                       apptTime <= activeAttendance.Shift.EndTime;
            }).ToList();

            // Exclude Cancelled and ExpiredPayment from validation
            var validAppointments = shiftAppointments
                .Where(a => a.Status != "Cancelled" && a.Status != "ExpiredPayment")
                .ToList();

            var incompleteAppointments = validAppointments
                .Where(a => a.Status != "Completed" && a.Status != "Hospitalized")
                .ToList();

            if (incompleteAppointments.Any())
            {
                _logger.LogWarning("Doctor {DoctorId} cannot checkout - {Count} appointments not completed", 
                    doctorId, incompleteAppointments.Count);
                
                return BadRequest(new { 
                    message = $"Còn {incompleteAppointments.Count} bệnh nhân chưa được khám xong! Vui lòng hoàn thành tất cả lịch hẹn trước khi check-out.",
                    incompleteCount = incompleteAppointments.Count
                });
            }

            // Update attendance with checkout time
            activeAttendance.CheckOutTime = nowUtc;
            activeAttendance.Status = "CheckedOut";
            activeAttendance.CheckOutNote = request.Note;
            activeAttendance.UpdatedAt = nowUtc;
            
            await _attendanceRepository.UpdateAsync(activeAttendance);

            doctor.Status &= ~DoctorStatus.OffDuty;
            doctor.UpdatedAt = nowUtc;
            await _doctorRepository.UpdateAsync(doctor);

            _logger.LogInformation("Doctor {DoctorId} checked out at {Time}", doctorId, nowVn);

            return Ok(new
            {
                message = "Check-out thành công!",
                attendance = activeAttendance,
                checkOutTime = nowVn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-out");
            return StatusCode(500, new { message = "Lỗi khi check-out" });
        }
    }

    [HttpGet("my-attendances")]
    public async Task<IActionResult> GetMyAttendances([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
            if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
            {
                return BadRequest(new { message = "Doctor ID not found in token" });
            }

            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var attendances = await _attendanceRepository.GetDoctorAttendancesAsync(doctorId, start, end);

            return Ok(attendances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attendances");
            return StatusCode(500, new { message = "Lỗi khi tải dữ liệu" });
        }
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetTodayAttendance()
    {
        try
        {
            var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
            if (string.IsNullOrEmpty(doctorIdClaim) || !int.TryParse(doctorIdClaim, out int doctorId))
            {
                return BadRequest(new { message = "Doctor ID not found in token" });
            }

            // First, try to get the active (not checked out) attendance
            var activeAttendance = await _attendanceRepository.GetActiveAttendanceAsync(doctorId);
            
            if (activeAttendance != null)
            {
                // Load shift info if needed
                if (activeAttendance.Shift == null)
                {
                    activeAttendance.Shift = await _shiftRepository.GetByIdAsync(activeAttendance.ShiftId);
                }
                return Ok(activeAttendance);
            }
            
            // If no active attendance, get the most recent attendance for today
            var nowUtc = DateTime.UtcNow;
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var today = DateTime.SpecifyKind(nowVn.Date, DateTimeKind.Utc);
            
            var attendance = await _attendanceRepository.GetTodayAttendanceAsync(doctorId, today);

            if (attendance == null)
            {
                return NotFound(new { message = "Chưa có dữ liệu chấm công hôm nay" });
            }

            return Ok(attendance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching today's attendance");
            return StatusCode(500, new { message = "Lỗi khi tải dữ liệu" });
        }
    }
}

public class CheckInRequest
{
    public string? Note { get; set; }
}

public class CheckOutRequest
{
    public string? Note { get; set; }
}
