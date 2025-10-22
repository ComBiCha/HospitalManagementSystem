using HospitalManagementSystem.Domain.Entities;
using System.Linq.Expressions;

namespace HospitalManagementSystem.Domain.Specifications
{
    public class MedicalRecordForPatientSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordForPatientSpecification(int patientId)
            : base(mr => mr.PatientId == patientId)
        {
            AddInclude(mr => mr.Doctor);
            AddInclude(mr => mr.Appointment);
        }
    }

    public class MedicalRecordBySpecialtySpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordBySpecialtySpecification(string specialty)
            : base(mr => mr.Doctor.Specialty == specialty)
        {
        }
    }

    public class MedicalRecordByDoctorNameSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordByDoctorNameSpecification(string doctorName)
            : base(mr => mr.Doctor.Name.Contains(doctorName))
        {
        }
    }

    public class MedicalRecordByDateRangeSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordByDateRangeSpecification(DateTime startDate, DateTime endDate)
            : base(mr => mr.Appointment.Date >= startDate && mr.Appointment.Date <= endDate)
        {
        }
    }
}
