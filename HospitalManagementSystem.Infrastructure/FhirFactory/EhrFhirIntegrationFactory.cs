using HospitalManagementSystem.Domain.Fhir;
using HospitalManagementSystem.Infrastructure.Epic;
using HospitalManagementSystem.Infrastructure.Cerner;

namespace HospitalManagementSystem.Infrastructure.FhirFactory
{
    public class EhrFhirIntegrationFactory
    {
        private readonly EpicFhirIntegrationService _epicService;
        private readonly CernerFhirIntegrationService _cernerService;

        public EhrFhirIntegrationFactory(EpicFhirIntegrationService epic, CernerFhirIntegrationService cerner)
        {
            _epicService = epic;
            _cernerService = cerner;
        }

        public IEhrFhirIntegrationService? GetService(EHRSystem ehrSystem)
        {
            return ehrSystem switch
            {
                EHRSystem.Epic => _epicService,
                EHRSystem.Cerner => _cernerService,
                _ => null
            };
        }
    }
}