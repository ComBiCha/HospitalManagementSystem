namespace HospitalManagementSystem.Domain.Entities
{
    public class Appointment
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
    }

    public enum AppointmentStatus
    {
        Scheduled,
        Completed,
        Cancelled,
        Rescheduled
    }
}
