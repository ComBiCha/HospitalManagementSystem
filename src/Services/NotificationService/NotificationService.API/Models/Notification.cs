namespace NotificationService.API.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string Type { get; set; } = "Email"; // Email, SMS, Push
        public string RecipientEmail { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
    }
}
