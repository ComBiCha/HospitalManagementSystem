
using HospitalManagementSystem.Domain.Entities;
using System;

namespace HospitalManagementSystem.Application.DTOs.Appointment
{
    public class AppointmentResultDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public AppointmentType Type { get; set; }
        public decimal BookingFee { get; set; }
        public int? BookingPaymentId { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
    }
}
