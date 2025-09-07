using System;

namespace PatientService.API.Models
{
    [Flags]
    public enum PatientStatus
    {
        None = 0,           // 0000 0000
        Active = 1,         // 0000 0001
        Inactive = 2,       // 0000 0010
        InTreatment = 4,    // 0000 0100
        Emergency = 8,      // 0000 1000
        Admitted = 16,      // 0001 0000
        Discharged = 32,    // 0010 0000
        Transferred = 64,   // 0100 0000
        OnHold = 128        // 1000 0000
    }
}