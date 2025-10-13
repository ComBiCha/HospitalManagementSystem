namespace HospitalManagementSystem.Application.DTOs.Appointment
{
    public class AppointmentFilterDto
    {
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
