namespace HospitalManagementSystem.Domain.FhirEpic
{
    public interface IFhirEpicIntegrationService
    {
        Task<string> GetPatientDemographicsAsync(string patientId);
        Task<string> GetAppointmentsAsync(string patientId);
        Task<string> GetMedicationsAsync(string patientId);

    }
}