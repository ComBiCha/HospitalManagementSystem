namespace HospitalManagementSystem.Domain.Events
{
    public class AppointmentCreatedEvent : IDomainEvent
    {
        public int AppointmentId { get; }
        public int PatientId { get; }
        public string PatientName { get; }
        public int DoctorId { get; }
        public string DoctorName { get; }
        public string DoctorSpecialty { get; }
        public DateTime Date { get; }
        public string Status { get; }
        public DateTime CreatedAt { get; }
        public int CreatedByUserId { get; }
        public string CreatedByRole { get; }
        public DateTime OccurredOn { get; }

        public AppointmentCreatedEvent(
            int appointmentId,
            int patientId,
            string patientName,
            int doctorId,
            string doctorName,
            string doctorSpecialty,
            DateTime date,
            string status,
            DateTime createdAt,
            int createdByUserId,
            string createdByRole)
        {
            AppointmentId = appointmentId;
            PatientId = patientId;
            PatientName = patientName;
            DoctorId = doctorId;
            DoctorName = doctorName;
            DoctorSpecialty = doctorSpecialty;
            Date = date;
            Status = status;
            CreatedAt = createdAt;
            CreatedByUserId = createdByUserId;
            CreatedByRole = createdByRole;
            OccurredOn = DateTime.UtcNow;
        }
    }
}