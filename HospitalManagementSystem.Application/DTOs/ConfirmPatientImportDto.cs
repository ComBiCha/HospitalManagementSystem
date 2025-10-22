namespace HospitalManagementSystem.Application.DTOs
{
    public class ConfirmPatientImportDto
    {
        public string EpicPatientId { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Email { get; set; }
        public DateTime DateOfBirth { get; set; }
    }
}