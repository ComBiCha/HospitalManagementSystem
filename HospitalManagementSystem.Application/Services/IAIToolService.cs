using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HospitalManagementSystem.Application.DTOs.AITools;

namespace HospitalManagementSystem.Application.Services
{
    public interface IAIToolService
    {
        Task<IEnumerable<string>> GetSpecialties();
        Task<IEnumerable<DoctorInfoDTO>> GetDoctorsBySpecialty(string? specialty);
        Task<IEnumerable<string>> GetAvailableDatesForDoctor(int doctorId);
        Task<IEnumerable<string>> GetAvailableSlotsForDoctor(int doctorId, string date);
        Task<BookAppointmentResponseDTO> BookAppointment(BookAppointmentRequestDTO request);
    }
}