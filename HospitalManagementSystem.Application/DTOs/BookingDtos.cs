namespace HospitalManagementSystem.Application.DTOs
{
    public class TimeSlotDto
    {
        public TimeSpan Time { get; set; }
        public string DisplayTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class AvailableDoctorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class DoctorShiftDto
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}