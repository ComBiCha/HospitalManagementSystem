namespace HospitalManagementSystem.Domain.Events
{
    public class AppointmentUpdatedEvent
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialty { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public int UpdatedByUserId { get; set; } 
        public string UpdatedByRole { get; set; } = string.Empty;
    }
}