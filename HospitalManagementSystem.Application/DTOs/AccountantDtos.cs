namespace HospitalManagementSystem.Application.DTOs
{
    public class UnpaidMedicalRecordDto
    {
        public int MedicalRecordId { get; set; }
        public int AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int PatientId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public decimal TotalFee { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public int? PendingPaymentId { get; set; }
        public string? PendingPaymentMethod { get; set; }
    }

    public class PaymentDto
    {
        public int Id { get; set; }
        public int? MedicalRecordId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
