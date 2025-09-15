namespace HospitalManagementSystem.Domain.Fhir
{
    public interface IEhrFhirIntegrationService
    {
        Task<string> GetPatientDemographicsAsync(string patientId);
    }
}