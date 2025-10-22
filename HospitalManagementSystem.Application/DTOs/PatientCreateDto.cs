namespace HospitalManagementSystem.Application.DTOs
{ 
    public class PatientCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<PatientIdentifierDto> Identifiers { get; set; } = new();
    }
}