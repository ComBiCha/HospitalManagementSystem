namespace HospitalManagementSystem.Domain.Fhir
{
    public interface IEhrFhirIntegrationService
    {
        Task<string?> GetPatientDemographicsAsync(string patientId);
        Task<string> SearchPatientsAsync (string? name = null, string? email = null,
        string? phone = null, string? gender = null, string? identifier = null);
        Task<(bool, string)> VerifyPatientExistsAsync(string patientId);

        // Methods for Clinical Decision Support
        Task<string> GetMedicationRequestsAsync(string patientId);
        Task<string> GetMedicationStatementsAsync(string patientId);
        Task<string> GetAllergyIntolerancesAsync(string patientId);
        Task<string> GetConditionsAsync(string patientId);
        Task<string> GetObservationsAsync(string patientId, string? category = null);
    }
}