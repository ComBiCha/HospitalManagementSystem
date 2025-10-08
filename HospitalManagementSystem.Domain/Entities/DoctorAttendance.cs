namespace HospitalManagementSystem.Domain.Entities;

public class DoctorAttendance
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int ShiftId { get; set; }
    public DateTime ShiftDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string Status { get; set; } = "Scheduled"; // Scheduled, CheckedIn, CheckedOut, Absent
    public string? CheckInNote { get; set; }
    public string? CheckOutNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Doctor? Doctor { get; set; }
    public DoctorShift? Shift { get; set; }
}
