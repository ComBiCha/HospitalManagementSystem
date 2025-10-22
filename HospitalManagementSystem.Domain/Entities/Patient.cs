using System;
using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Domain.Entities
{
    public class Patient
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        [Range(0, 150)]
        public int Age 
        {
            get 
            {
                if (!DateOfBirth.HasValue) return 0;
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
            set { /* Setter is only for EF Core, no logic needed */ }
        }

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
        
        public ICollection<PatientIdentifiers> PatientIdentifiers { get; set; } = new List<PatientIdentifiers>();
    }
}