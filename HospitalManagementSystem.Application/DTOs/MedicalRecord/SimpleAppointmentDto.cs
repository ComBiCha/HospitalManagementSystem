namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class SimpleAppointmentDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}