using HospitalManagementSystem.Domain.Entities;
using System.Linq.Expressions;

namespace HospitalManagementSystem.Domain.Specifications
{
    public class AppointmentForPatientSpecification : BaseSpecification<Appointment>
    {
        public AppointmentForPatientSpecification(int patientId) 
            : base(a => a.PatientId == patientId)
        {
        }
    }

    public class AppointmentByStatusSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByStatusSpecification(string status)
            : base(a => a.Status == status)
        {
        }
    }

    public class AppointmentByDateRangeSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByDateRangeSpecification(DateTime startDate, DateTime endDate)
            : base(a => a.Date >= startDate && a.Date <= endDate)
        {
        }
    }
}
