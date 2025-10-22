namespace HospitalManagementSystem.Application.DTOs
{
    public class ExternalPatientHistoryDto
    {
        public string? MedicationRequests { get; set; }
        public string? MedicationStatements { get; set; }
        public string? AllergyIntolerances { get; set; }
        public string? Conditions { get; set; }
        public string? Observations { get; set; }
    }
}
