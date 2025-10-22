
namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class MedicalRecordFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Specialty { get; set; }
        public string? DoctorName { get; set; }
    }
}
