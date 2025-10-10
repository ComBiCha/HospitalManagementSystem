namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class CanStartExaminationResultDto
    {
        public bool CanStart { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? MedicalRecordId { get; set; }
        public bool IsReadOnly { get; set; }
    }
}