using System;

namespace HospitalManagementSystem.Application.DTOs.AITools;

public class BookAppointmentRequestDTO
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime DateTime { get; set; }
}
