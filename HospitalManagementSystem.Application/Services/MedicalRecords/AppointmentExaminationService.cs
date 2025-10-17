using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs.MedicalRecord;
using HospitalManagementSystem.Domain.Entities;
using System.Linq;

public class AppointmentExaminationService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorAttendanceRepository _attendanceRepository;
    private readonly IMedicalRecordRepository _medicalRecordRepository;
    private readonly IPaymentRepository _paymentRepository;

    public AppointmentExaminationService(
        IAppointmentRepository appointmentRepository,
        IDoctorAttendanceRepository attendanceRepository,
        IMedicalRecordRepository medicalRecordRepository,
        IPaymentRepository paymentRepository)
    {
        _appointmentRepository = appointmentRepository;
        _attendanceRepository = attendanceRepository;
        _medicalRecordRepository = medicalRecordRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<CanStartExaminationResultDto> CanStartExaminationAsync(int appointmentId, int doctorId)
    {
        var appointment = await _appointmentRepository.GetByIdWithDetailsAsync(appointmentId);
        if (appointment == null)
            return new CanStartExaminationResultDto { CanStart = false, Message = "Appointment not found" };

        if (appointment.DoctorId != doctorId)
            return new CanStartExaminationResultDto { CanStart = false, Message = "You can only examine your own appointments" };

        if (appointment.Status == "Completed")
        {
            return new CanStartExaminationResultDto
            {
                CanStart = true,
                Message = "Đã hoàn thành khám bệnh - Xem hồ sơ",
                MedicalRecordId = appointment.MedicalRecord?.Id,
                IsReadOnly = true
            };
        }

        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        var today = nowVn.Date;
        var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);

        var activeAttendance = await _attendanceRepository.GetActiveAttendanceAsync2(doctorId, todayUtc);
        if (activeAttendance == null)
        {
            return new CanStartExaminationResultDto
            {
                CanStart = false,
                Message = "Bác sĩ chưa check-in!"
            };
        }

        var appointmentTime = appointment.Date;
        var tenMinutesBefore = appointmentTime.AddMinutes(-30);
        var nowUtc = DateTime.UtcNow;

        if (nowUtc < tenMinutesBefore)
        {
            return new CanStartExaminationResultDto
            {
                CanStart = false,
                Message = $"Chỉ có thể bắt đầu khám từ {TimeZoneInfo.ConvertTimeFromUtc(tenMinutesBefore, vnTimeZone):HH:mm}"
            };
        }

        if (appointment.Status == "Hospitalized")
        {
            return new CanStartExaminationResultDto
            {
                CanStart = true,
                Message = "Bệnh nhân đang nhập viện - có thể tiếp tục điều trị",
                MedicalRecordId = appointment.MedicalRecord?.Id,
                IsReadOnly = false
            };
        }

        if (appointment.Status != "Scheduled" && appointment.Status != "InProgress")
        {
            return new CanStartExaminationResultDto
            {
                CanStart = false,
                Message = $"Appointment status: {appointment.Status}"
            };
        }

        return new CanStartExaminationResultDto
        {
            CanStart = true,
            Message = "Có thể bắt đầu khám",
            MedicalRecordId = appointment.MedicalRecord?.Id,
            IsReadOnly = false
        };
    }

    public async Task<(string Message, MedicalRecordDto? MedicalRecord)> StartExaminationAsync(int appointmentId, int doctorId)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
            return ("Appointment not found", null);

        if (appointment.DoctorId != doctorId)
            return ("You can only examine your own appointments", null);

        var existingRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(appointmentId);
        if (existingRecord != null)
        {
            var dto = MapToDto(existingRecord);
            return ("Medical record already exists", dto);
        }

        var completedDeposits = await _paymentRepository.GetCompletedDepositsByAppointmentIdAsync(appointment.Id);
        var totalDeposited = completedDeposits.Sum(p => p.Amount);

        var medicalRecord = new MedicalRecord
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = doctorId,
            Diagnosis = "[]",
            Symptoms = "[]",
            Treatment = "",
            Prescription = "[]",
            Notes = "",
            ConsultationFee = 200000, // Default consultation fee
            MedicineFee = 0,
            TestFee = 0,
            OtherFee = 0,
            PaidAmount = totalDeposited, // Apply deposited amount
            CreatedAt = DateTime.UtcNow
        };

        // Determine payment status based on the new rule
        if (medicalRecord.PaidAmount >= medicalRecord.TotalFee)
        {
            medicalRecord.PaymentStatus = "Paid";
        }
        else
        {
            medicalRecord.PaymentStatus = "Unpaid";
        }

        var createdRecord = await _medicalRecordRepository.CreateAsync(medicalRecord);

        // Link deposits to the new medical record
        foreach (var deposit in completedDeposits)
        {
            deposit.MedicalRecordId = createdRecord.Id;
            await _paymentRepository.UpdateAsync(deposit);
        }

        appointment.Status = "InProgress";
        appointment.UpdatedAt = DateTime.UtcNow;
        await _appointmentRepository.UpdateAsync(appointment);

        var response = MapToDto(createdRecord);
        return ("Bắt đầu khám bệnh thành công", response);
    }

    private MedicalRecordDto MapToDto(MedicalRecord record)
    {
        return new MedicalRecordDto
        {
            Id = record.Id,
            AppointmentId = record.AppointmentId,
            PatientId = record.PatientId,
            DoctorId = record.DoctorId,
            Diagnosis = record.Diagnosis,
            Symptoms = record.Symptoms,
            Treatment = record.Treatment,
            Prescription = record.Prescription,
            Notes = record.Notes,
            ConsultationFee = record.ConsultationFee,
            MedicineFee = record.MedicineFee,
            TestFee = record.TestFee,
            OtherFee = record.OtherFee,
            PaidAmount = record.PaidAmount,
            PaymentStatus = record.PaymentStatus,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}