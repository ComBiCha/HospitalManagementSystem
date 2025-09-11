using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Application.DTOs
{ 
    public class PatientIdentifierDto
    {
        public EHRSystem EHRSystem { get; set; } = EHRSystem.Epic;
        public string ExternalId { get; set; } = string.Empty;
        public string IdentifierType { get; set; } = string.Empty;
    }
}