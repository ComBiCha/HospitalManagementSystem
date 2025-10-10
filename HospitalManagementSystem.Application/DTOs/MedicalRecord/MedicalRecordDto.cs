namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
     public class MedicalRecordDto
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;
        public string Prescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal ConsultationFee { get; set; }
        public decimal MedicineFee { get; set; }
        public decimal TestFee { get; set; }
        public decimal OtherFee { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public PatientDto? Patient { get; set; }
        public DoctorDto? Doctor { get; set; }
        public SimpleAppointmentDto? Appointment { get; set; }
    }
}