namespace HospitalManagementSystem.Domain.Fhir
{
    public interface IEhrFhirIntegrationService
    {
        Task<string> GetPatientDemographicsAsync(string patientId);
        Task<string> SearchPatientsAsync (string? name = null, string? email = null,
        string? phone = null, string? gender = null, string? identifier = null);
    }
}