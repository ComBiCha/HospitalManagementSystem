using HospitalManagementSystem.Domain.Fhir;
using HospitalManagementSystem.Infrastructure.FhirFactory;

namespace HospitalManagementSystem.Application.Services
{
    public class EhrFhirApplicationService
    {
        private readonly EhrFhirIntegrationFactory _factory;

        public EhrFhirApplicationService(EhrFhirIntegrationFactory factory)
        {
            _factory = factory;
        }

        public async Task<object?> GetPatientDemographicsAsync(string patientId, EHRSystem ehrSystem)
        {
            var service = _factory.GetService(ehrSystem);
            if (service == null) return null;
            return await service.GetPatientDemographicsAsync(patientId);
        }
    }
}