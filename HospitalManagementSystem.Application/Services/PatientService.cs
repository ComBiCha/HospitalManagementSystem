using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Domain.Fhir;
using HospitalManagementSystem.Infrastructure.FhirFactory;
using System.Xml.Linq;

public class PatientService
{
    private readonly IPatientRepository _patientRepository;
    private readonly EhrFhirIntegrationFactory _ehrFhirFactory;

    public PatientService(IPatientRepository patientRepository, EhrFhirIntegrationFactory ehrFhirFactory)
    {
        _patientRepository = patientRepository;
        _ehrFhirFactory = ehrFhirFactory;
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

        var service = _ehrFhirFactory.GetService(ehrSystem);
        if (service == null)
            return null;

        var result = await service.GetPatientDemographicsAsync(identifier.ExternalId);

        if (ehrSystem == EHRSystem.Epic && result is string xml)
        {
            return ParseEpicPatientXml(xml);
        }
        return result;
    }
    public async Task<(List<Patient> Data, string? NextLink, string? PreviousLink)> GetPatientsWithNextLinkAsync(int? lastId = null, int pageSize = 20, string baseUrl = "/api/patients")
    {
        return await _patientRepository.GetPatientsWithNextLinkAsync(lastId, pageSize, baseUrl);
    }
    public async Task<string> SearchPatientsInEhrAsync(EHRSystem ehrSystem,
    string? name = null,
    string? email = null,
    string? phone = null,
    string? gender = null,
    string? identifier = null)
    {
        var service = _ehrFhirFactory.GetService(ehrSystem);
        if (service == null)
            throw new Exception($"EHR service '{ehrSystem}' not found");

        return await service.SearchPatientsAsync(name, email, phone, gender, identifier);
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
    public async Task<PatientIdentifiers> AddPatientIdentifierAsync(PatientIdentifiers identifier)
    {
        return await _patientRepository.AddPatientIdentifierAsync(identifier);
    }

    public async Task<PatientIdentifiers?> UpdatePatientIdentifierAsync(PatientIdentifiers identifier)
    {
        return await _patientRepository.UpdatePatientIdentifierAsync(identifier);
    }

    public async Task<bool> DeletePatientIdentifierAsync(int identifierId)
    {
        return await _patientRepository.DeletePatientIdentifierAsync(identifierId);
    }
}