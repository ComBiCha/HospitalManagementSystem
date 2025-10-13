using System;

namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class PatientMedicalRecordDto
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialty { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;
        public string Prescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal ConsultationFee { get; set; }
        public decimal MedicineFee { get; set; }
        public decimal TestFee { get; set; }
        public decimal OtherFee { get; set; }
        public decimal TotalFee => ConsultationFee + MedicineFee + TestFee + OtherFee;
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string AppointmentStatus { get; set; } = string.Empty;
    }
}
