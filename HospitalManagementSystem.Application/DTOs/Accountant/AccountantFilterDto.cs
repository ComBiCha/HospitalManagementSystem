namespace HospitalManagementSystem.Application.DTOs.Accountant
{
    public class AccountantFilterDto
    {
        public string? PatientName { get; set; }
        public int? MedicalRecordId { get; set; }
        public int? AppointmentId { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
