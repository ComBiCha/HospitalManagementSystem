using System;
using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Domain.Entities
{
    public class PatientIdentifiers
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int PatientId { get; set; }
        public Patient? Patient { get; set; }
        [Required, MaxLength(50)]
        public EHRSystem EHRSystem { get; set; } = EHRSystem.Epic;
        [Required, MaxLength(100)]
        public string ExternalId { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string IdentifierType { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

    }
}