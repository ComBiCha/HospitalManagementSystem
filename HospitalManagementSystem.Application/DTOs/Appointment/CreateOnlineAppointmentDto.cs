namespace HospitalManagementSystem.Application.DTOs.Appointment
{
    public class CreateOnlineAppointmentDto
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime Date { get; set; }
    }
}