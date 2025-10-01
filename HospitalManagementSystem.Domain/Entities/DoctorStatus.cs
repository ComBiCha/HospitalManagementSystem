using System;

namespace HospitalManagementSystem.Domain.Entities
{
    [Flags]
    public enum DoctorStatus
    {
        None = 0,           // 0000 0000
        Active = 1,         // 0000 0001
        Inactive = 2,       // 0000 0010
        OnDuty = 4,         // 0000 0100
        OffDuty = 8,        // 0000 1000
        OnLeave = 16,       // 0001 0000
        OnCall = 32,        // 0010 0000
        InSurgery = 64,     // 0100 0000
        OnVacation = 128    // 1000 0000
    }
}