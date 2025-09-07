using System;
using System.ComponentModel.DataAnnotations;

namespace PatientService.API.Models
{
    public class Patient
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Range(0, 150)]
        public int Age { get; set; }
        
        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;
        
        public PatientStatus Status { get; set; } = PatientStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        public bool HasStatus(PatientStatus status) => Status.HasFlag(status);
        
        public void AddStatus(PatientStatus status) => Status |= status;
        
        public void RemoveStatus(PatientStatus status) => Status &= ~status;
        
        public bool IsActive => HasStatus(PatientStatus.Active);
        public bool IsInTreatment => HasStatus(PatientStatus.InTreatment);
        public bool IsEmergency => HasStatus(PatientStatus.Emergency);
        public bool IsAdmitted => HasStatus(PatientStatus.Admitted);
        public bool IsDischarged => HasStatus(PatientStatus.Discharged);
        public bool IsOnHold => HasStatus(PatientStatus.OnHold);
    }
}