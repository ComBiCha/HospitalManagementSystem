using HospitalManagementSystem.Domain.Fhir;
using HospitalManagementSystem.Infrastructure.FhirFactory;
using HospitalManagementSystem.Application.DTOs;
using System.Text.Json;

namespace HospitalManagementSystem.Application.Services
{
    public class EhrFhirApplicationService
    {
        private readonly EhrFhirIntegrationFactory _factory;

        public EhrFhirApplicationService(EhrFhirIntegrationFactory factory)
        {
            _factory = factory;
        }

        public async Task<PatientImportPreviewDto?> GetPatientDemographicsAsync(string patientId, EHRSystem ehrSystem)
        {
            var service = _factory.GetService(ehrSystem);
            if (service == null) return null;

            var jsonString = await service.GetPatientDemographicsAsync(patientId);
            if (string.IsNullOrEmpty(jsonString)) return null;

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var dto = new PatientImportPreviewDto { EpicPatientId = patientId };

            // Extract Name
            if (root.TryGetProperty("name", out var nameArray) && nameArray.GetArrayLength() > 0)
            {
                var first_name = nameArray[0];
                if (first_name.TryGetProperty("given", out var givenArray) && givenArray.GetArrayLength() > 0)
                {
                    dto.FirstName = givenArray[0].GetString();
                }
                if (first_name.TryGetProperty("family", out var familyElement))
                {
                    dto.LastName = familyElement.GetString();
                }
            }

            // Extract Email
            if (root.TryGetProperty("telecom", out var telecomArray))
            {
                foreach (var contactPoint in telecomArray.EnumerateArray())
                {
                    if (contactPoint.TryGetProperty("system", out var system) && system.GetString() == "email")
                    {
                        if (contactPoint.TryGetProperty("value", out var value))
                        {
                            dto.Email = value.GetString();
                            break; // Found email, no need to continue
                        }
                    }
                }
            }

            // Extract Date of Birth
            if (root.TryGetProperty("birthDate", out var birthDateElement) && birthDateElement.GetString() != null)
            {
                dto.DateOfBirth = birthDateElement.GetDateTime();
            }

            return dto;
        }

        public async Task<(bool, string)> VerifyPatientExistsAsync(string patientId, EHRSystem ehrSystem)
        {
            var service = _factory.GetService(ehrSystem);
            if (service == null) 
            {
                throw new NotSupportedException($"EHR system '{ehrSystem}' is not supported.");
            }
            return await service.VerifyPatientExistsAsync(patientId);
        }

        public async Task<ExternalPatientHistoryDto> GetExternalPatientHistoryAsync(string patientId, EHRSystem ehrSystem, List<string>? resourceTypes = null)
        {
            var service = _factory.GetService(ehrSystem);
            if (service == null) 
            {
                throw new NotSupportedException($"EHR system '{ehrSystem}' is not supported.");
            }

            var history = new ExternalPatientHistoryDto();
            bool fetchAll = resourceTypes == null || !resourceTypes.Any();

            if (fetchAll || resourceTypes.Contains("MedicationRequest"))
            {
                history.MedicationRequests = await service.GetMedicationRequestsAsync(patientId);
            }
            if (fetchAll || resourceTypes.Contains("MedicationStatement"))
            {
                history.MedicationStatements = await service.GetMedicationStatementsAsync(patientId);
            }
            if (fetchAll || resourceTypes.Contains("AllergyIntolerance"))
            {
                history.AllergyIntolerances = await service.GetAllergyIntolerancesAsync(patientId);
            }
            if (fetchAll || resourceTypes.Contains("Condition"))
            {
                history.Conditions = await service.GetConditionsAsync(patientId);
            }
            if (fetchAll || resourceTypes.Contains("Observation"))
            {
                history.Observations = await service.GetObservationsAsync(patientId, "laboratory");
            }

            return history;
        }
    }
}