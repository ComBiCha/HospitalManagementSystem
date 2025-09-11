using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Domain.FhirEpic;
using System.Xml.Linq;

public class PatientService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IFhirEpicIntegrationService _fhirEpicIntegrationService;

    public PatientService(IPatientRepository patientRepository, IFhirEpicIntegrationService fhirEpicIntegrationService)
    {
        _patientRepository = patientRepository;
        _fhirEpicIntegrationService = fhirEpicIntegrationService;
    }

    public async Task<Patient> CreatePatientAsync(PatientCreateDto dto)
    {
        var patient = new Patient
        {
            Name = dto.Name,
            Age = dto.Age,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            PatientIdentifiers = dto.Identifiers.Select(x => new PatientIdentifiers
            {
                EHRSystem = x.EHRSystem,
                ExternalId = x.ExternalId,
                IdentifierType = x.IdentifierType,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        return await _patientRepository.CreatePatientAsync(patient);
    }

    public async Task<List<PatientIdentifiers>> GetPatientIdentifiersAsync(int patientId)
    {
        var identifiers = await _patientRepository.GetPatientIdentifiersAsync(patientId);
        return identifiers ?? new List<PatientIdentifiers>();
    }

    public async Task<object?> GetPatientInfoFromEhrAsync(int patientId, EHRSystem ehrSystem)
    {
        var identifiers = await _patientRepository.GetPatientIdentifiersAsync(patientId);
        if (identifiers == null || identifiers.Count == 0)
            return null;

        var identifier = identifiers
            .FirstOrDefault(x => x.EHRSystem == ehrSystem && x.IdentifierType == "FHIR" && x.IsActive);

        if (identifier == null)
            return null;

        if (ehrSystem == EHRSystem.Epic)
        {
                var xml = await _fhirEpicIntegrationService.GetPatientDemographicsAsync(identifier.ExternalId);
                return ParseEpicPatientXml(xml);
        }
        // else if (ehrSystem == EHRSystem.Cerner) { ... }
        // else if (ehrSystem == EHRSystem.Meditech) { ... }

        return null;
    }

    public object ParseEpicPatientXml(string xml)
    {
        XNamespace ns = "http://hl7.org/fhir";
        var doc = XDocument.Parse(xml);

        var patient = doc.Element(ns + "Patient");
        if (patient == null) return null;

        var id = patient.Element(ns + "id")?.Attribute("value")?.Value;
        var nameElement = patient.Elements(ns + "name").FirstOrDefault();
        var name = nameElement?.Element(ns + "text")?.Attribute("value")?.Value;
        var gender = patient.Element(ns + "gender")?.Attribute("value")?.Value;
        var birthDate = patient.Element(ns + "birthDate")?.Attribute("value")?.Value;
        var email = patient.Elements(ns + "telecom")
            .FirstOrDefault(t => t.Element(ns + "system")?.Attribute("value")?.Value == "email")
            ?.Element(ns + "value")?.Attribute("value")?.Value;
        var phone = patient.Elements(ns + "telecom")
            .FirstOrDefault(t => t.Element(ns + "system")?.Attribute("value")?.Value == "phone")
            ?.Element(ns + "value")?.Attribute("value")?.Value;
        var address = patient.Element(ns + "address")?.Element(ns + "text")?.Attribute("value")?.Value;

        return new
        {
            Id = id,
            Name = name,
            Gender = gender,
            BirthDate = birthDate,
            Email = email,
            Phone = phone,
            Address = address
        };
    }
}