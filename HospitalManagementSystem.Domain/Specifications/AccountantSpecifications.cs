using HospitalManagementSystem.Domain.Entities;
using System;

namespace HospitalManagementSystem.Domain.Specifications
{
    public class EligibleForDepositSpecification : BaseSpecification<Appointment>
    {
        public EligibleForDepositSpecification()
            : base(a => a.Status != "Completed" && a.Status != "Cancelled" && a.Status != "ExpiredPayment" && a.Status != "PendingPayment")
        {
            AddInclude(a => a.Patient);
            AddInclude(a => a.Doctor);
        }
    }

    public class UnpaidMedicalRecordSpecification : BaseSpecification<MedicalRecord>
    {
        public UnpaidMedicalRecordSpecification()
            : base(mr => (mr.PaymentStatus == "Unpaid" || mr.PaymentStatus == "PartiallyPaid") && mr.Appointment.Status == "Completed")
        {
            AddInclude(mr => mr.Patient);
            AddInclude(mr => mr.Doctor);
            AddInclude(mr => mr.Appointment);
        }
    }

    public class RefundableMedicalRecordSpecification : BaseSpecification<MedicalRecord>
    {
        public RefundableMedicalRecordSpecification()
            : base(mr => mr.Appointment.Status == "Completed" && mr.PaidAmount > (mr.ConsultationFee + mr.MedicineFee + mr.TestFee + mr.OtherFee))
        {
            AddInclude(mr => mr.Patient);
            AddInclude(mr => mr.Doctor);
            AddInclude(mr => mr.Appointment);
        }
    }

    public class AppointmentByIdSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByIdSpecification(int id)
            : base(a => a.Id == id)
        {
        }
    }

    public class AppointmentByPatientNameSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByPatientNameSpecification(string patientName)
            : base(a => a.Patient.Name.Contains(patientName))
        {
            AddInclude(a => a.Patient);
        }
    }

    public class MedicalRecordByPatientNameSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordByPatientNameSpecification(string patientName)
            : base(mr => mr.Patient.Name.Contains(patientName))
        {
            AddInclude(mr => mr.Patient);
        }
    }

    public class MedicalRecordByIdSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordByIdSpecification(int id)
            : base(mr => mr.Id == id)
        {
        }
    }

    public class MedicalRecordByPaymentStatusSpecification : BaseSpecification<MedicalRecord>
    {
        public MedicalRecordByPaymentStatusSpecification(string status)
            : base(mr => mr.PaymentStatus == status)
        {
        }
    }
}
