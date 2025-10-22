namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class PatientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<PatientIdentifierDto>? PatientIdentifiers { get; set; }
    }
}