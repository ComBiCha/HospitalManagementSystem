using HospitalManagementSystem.Domain.FhirEpic;

namespace HospitalManagementSystem.Application.Services
{
    public class FhirEpicIntegrationService
    {
        private readonly IFhirEpicIntegrationService _fhirEpicService;

        public FhirEpicIntegrationService(IFhirEpicIntegrationService fhirEpicService)
        {
            _fhirEpicService = fhirEpicService;
        }

        public async Task<string> GetPatientDemographicsAsync(string patientId)
            => await _fhirEpicService.GetPatientDemographicsAsync(patientId);

        public async Task<string> GetAppointmentsAsync(string patientId)
            => await _fhirEpicService.GetAppointmentsAsync(patientId);

        public async Task<string> GetMedicationsAsync(string patientId)
            => await _fhirEpicService.GetMedicationsAsync(patientId);
    }
}