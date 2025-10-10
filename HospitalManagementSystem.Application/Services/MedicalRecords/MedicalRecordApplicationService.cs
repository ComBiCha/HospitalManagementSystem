using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs.MedicalRecord;
using HospitalManagementSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class MedicalRecordApplicationService
{
    private readonly ILogger<MedicalRecordApplicationService> _logger;
    private readonly IMedicalRecordRepository _medicalRecordRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPrescriptionItemRepository _prescriptionItemRepository;
    private readonly IMedicalRecordHistoryRepository _medicalRecordHistoryRepository;

    public MedicalRecordApplicationService(
        IMedicalRecordRepository medicalRecordRepository,
        IAppointmentRepository appointmentRepository,
        IPrescriptionItemRepository prescriptionItemRepository,
        IMedicalRecordHistoryRepository medicalRecordHistoryRepository,
        ILogger<MedicalRecordApplicationService> logger)
    {
        _medicalRecordRepository = medicalRecordRepository;
        _appointmentRepository = appointmentRepository;
        _prescriptionItemRepository = prescriptionItemRepository;
        _medicalRecordHistoryRepository = medicalRecordHistoryRepository;
        _logger = logger;
    }
    public async Task<MedicalRecordDto?> GetMedicalRecordByAppointmentAsync(int appointmentId, int doctorId)
    {
        var medicalRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(appointmentId);
        if (medicalRecord == null || medicalRecord.DoctorId != doctorId)
            return null;

        var paidAmount = medicalRecord.Payments
            .Where(p => p.Status == "Completed" &&
                        (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
            .Sum(p => p.Amount);

        return new MedicalRecordDto
        {
            Id = medicalRecord.Id,
            AppointmentId = medicalRecord.AppointmentId,
            PatientId = medicalRecord.PatientId,
            DoctorId = medicalRecord.DoctorId,
            Diagnosis = medicalRecord.Diagnosis,
            Symptoms = medicalRecord.Symptoms,
            Treatment = medicalRecord.Treatment,
            Prescription = medicalRecord.Prescription,
            Notes = medicalRecord.Notes,
            ConsultationFee = medicalRecord.ConsultationFee,
            MedicineFee = medicalRecord.MedicineFee,
            TestFee = medicalRecord.TestFee,
            OtherFee = medicalRecord.OtherFee,
            PaidAmount = paidAmount,
            PaymentStatus = medicalRecord.PaymentStatus,
            CreatedAt = medicalRecord.CreatedAt,
            UpdatedAt = medicalRecord.UpdatedAt,
            Patient = medicalRecord.Patient != null ? new PatientDto
            {
                Id = medicalRecord.Patient.Id,
                Name = medicalRecord.Patient.Name,
                Age = medicalRecord.Patient.Age,
                Email = medicalRecord.Patient.Email,
                Status = medicalRecord.Patient.Status.ToString()
            } : null,
            Doctor = medicalRecord.Doctor != null ? new DoctorDto
            {
                Id = medicalRecord.Doctor.Id,
                Name = medicalRecord.Doctor.Name,
                Specialty = medicalRecord.Doctor.Specialty,
                Email = medicalRecord.Doctor.Email
            } : null,
            Appointment = medicalRecord.Appointment != null ? new SimpleAppointmentDto
            {
                Id = medicalRecord.Appointment.Id,
                Date = medicalRecord.Appointment.Date,
                Status = medicalRecord.Appointment.Status
            } : null
        };
    }

    public async Task<MedicalRecordDto?> GetMedicalRecordByIdAsync(int id)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null) return null;

        var paidAmount = medicalRecord.Payments
            .Where(p => p.Status == "Completed" && 
                        (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
            .Sum(p => p.Amount);

        return MapToDto(medicalRecord, paidAmount);
    }
    
    public async Task<(string Message, MedicalRecordDto? MedicalRecord)> UpdateMedicalRecordAsync(int id, UpdateMedicalRecordRequest request)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null)
            return ("Medical record not found", null);

        if (request.Diagnosis != null) medicalRecord.Diagnosis = request.Diagnosis;
        if (request.Symptoms != null) medicalRecord.Symptoms = request.Symptoms;
        if (request.Treatment != null) medicalRecord.Treatment = request.Treatment;
        if (request.Prescription != null) medicalRecord.Prescription = request.Prescription;
        if (request.Notes != null) medicalRecord.Notes = request.Notes;
        if (request.MedicineFee.HasValue) medicalRecord.MedicineFee = request.MedicineFee.Value;
        if (request.TestFee.HasValue) medicalRecord.TestFee = request.TestFee.Value;
        if (request.OtherFee.HasValue) medicalRecord.OtherFee = request.OtherFee.Value;

        var paidAmount = medicalRecord.Payments
            .Where(p => p.Status == "Completed" && 
                        (p.PaymentType == "Deposit" || p.PaymentType == "FinalPayment"))
            .Sum(p => p.Amount);

        medicalRecord.PaidAmount = paidAmount;
        medicalRecord.UpdatedAt = DateTime.UtcNow;

        var history = new MedicalRecordHistory
        {
            MedicalRecordId = medicalRecord.Id,
            DoctorId = medicalRecord.DoctorId,
            Diagnosis = medicalRecord.Diagnosis,
            Symptoms = medicalRecord.Symptoms,
            Treatment = medicalRecord.Treatment,
            Prescription = medicalRecord.Prescription,
            Notes = medicalRecord.Notes,
            MedicineFee = medicalRecord.MedicineFee,
            TestFee = medicalRecord.TestFee,
            OtherFee = medicalRecord.OtherFee,
            Action = "Update",
            CreatedAt = DateTime.UtcNow
        };
        await _medicalRecordHistoryRepository.AddAsync(history);

        await SyncPrescriptionItemsAsync(medicalRecord.Id, request.Prescription ?? "[]");

        await _medicalRecordRepository.UpdateAsync(medicalRecord);
        await _medicalRecordHistoryRepository.SaveChangesAsync();

        return ("Cập nhật thành công", MapToDto(medicalRecord, paidAmount));
    }

    public async Task<(string Message, bool Success)> CompleteMedicalRecordAsync(int id)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null)
            return ("Medical record not found", false);

        var totalFee = medicalRecord.ConsultationFee + medicalRecord.MedicineFee +
                       medicalRecord.TestFee + medicalRecord.OtherFee;

        var paidAmount = medicalRecord.Payments
            .Where(p => p.Status == "Completed")
            .Sum(p => p.Amount);

        medicalRecord.PaidAmount = paidAmount;

        if (paidAmount >= totalFee)
            medicalRecord.PaymentStatus = "FullyPaid";
        else if (paidAmount > 0)
            medicalRecord.PaymentStatus = "PartiallyPaid";
        else
            medicalRecord.PaymentStatus = "Unpaid";

        if (medicalRecord.Appointment != null)
        {
            medicalRecord.Appointment.Status = "Completed";
            medicalRecord.Appointment.UpdatedAt = DateTime.UtcNow;
            await _appointmentRepository.UpdateAsync(medicalRecord.Appointment);
        }

        // Save history snapshot
        var history = new MedicalRecordHistory
        {
            MedicalRecordId = medicalRecord.Id,
            DoctorId = medicalRecord.DoctorId,
            Diagnosis = medicalRecord.Diagnosis,
            Symptoms = medicalRecord.Symptoms,
            Treatment = medicalRecord.Treatment,
            Prescription = medicalRecord.Prescription,
            Notes = medicalRecord.Notes,
            MedicineFee = medicalRecord.MedicineFee,
            TestFee = medicalRecord.TestFee,
            OtherFee = medicalRecord.OtherFee,
            Action = "Complete",
            CreatedAt = DateTime.UtcNow
        };
        await _medicalRecordHistoryRepository.AddAsync(history);

        await _medicalRecordRepository.UpdateAsync(medicalRecord);
        await _medicalRecordHistoryRepository.SaveChangesAsync();

        return ("Hoàn thành khám bệnh", true);
    }

    public async Task<(string Message, MedicalRecordDto? MedicalRecord)> HospitalizeMedicalRecordAsync(int id)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null)
            return ("Medical record not found", null);

        if (medicalRecord.Appointment != null)
        {
            medicalRecord.Appointment.Status = "Hospitalized";
            medicalRecord.Appointment.UpdatedAt = DateTime.UtcNow;
            await _appointmentRepository.UpdateAsync(medicalRecord.Appointment);
        }

        // Save history snapshot
        var history = new MedicalRecordHistory
        {
            MedicalRecordId = medicalRecord.Id,
            DoctorId = medicalRecord.DoctorId,
            Diagnosis = medicalRecord.Diagnosis,
            Symptoms = medicalRecord.Symptoms,
            Treatment = medicalRecord.Treatment,
            Prescription = medicalRecord.Prescription,
            Notes = medicalRecord.Notes,
            MedicineFee = medicalRecord.MedicineFee,
            TestFee = medicalRecord.TestFee,
            OtherFee = medicalRecord.OtherFee,
            Action = "Hospitalize",
            CreatedAt = DateTime.UtcNow
        };
        await _medicalRecordHistoryRepository.AddAsync(history);

        await _medicalRecordRepository.UpdateAsync(medicalRecord);
        await _medicalRecordHistoryRepository.SaveChangesAsync();

        var paidAmount = medicalRecord.Payments
            .Where(p => p.Status == "Completed")
            .Sum(p => p.Amount);

        return ("Đã chuyển nhập viện", MapToDto(medicalRecord, paidAmount));
    }

    private async Task SyncPrescriptionItemsAsync(int medicalRecordId, string prescriptionJson)
    {
        try
        {
            _logger.LogInformation("Syncing prescription items for MedicalRecord {Id}. JSON: {Json}", medicalRecordId, prescriptionJson);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var prescriptionList = JsonSerializer.Deserialize<List<PrescriptionItemDto>>(prescriptionJson, options);
            if (prescriptionList == null || prescriptionList.Count == 0)
            {
                _logger.LogWarning("No prescription items to sync");
                return;
            }

            foreach (var item in prescriptionList)
            {
                // Determine ItemType based on type field
                var itemType = item.Type?.ToLower() == "drug" ? "Medicine" : "Test";
                var itemCode = (item.Code ?? item.Name ?? "").Trim();
                
                if (string.IsNullOrEmpty(itemCode))
                {
                    _logger.LogWarning("Skipping item with empty code: {Item}", JsonSerializer.Serialize(item));
                    continue;
                }
                
                _logger.LogInformation("Processing item: Type={Type}, Code={Code}, Name={Name}", itemType, itemCode, item.Name);
                
                // Find existing item
                var existingItem = await _prescriptionItemRepository.GetByMedicalRecordAndCodeAsync(medicalRecordId, itemCode);

                if (existingItem == null)
                {
                    var newItem = new PrescriptionItem
                    {
                        MedicalRecordId = medicalRecordId,
                        ItemType = itemType,
                        ItemCode = itemCode,
                        ItemName = item.Name ?? "",
                        Quantity = item.Quantity,
                        Unit = item.Unit ?? (itemType == "Medicine" ? "viên" : "lần"),
                        Price = item.Fee ?? 0,
                        Status = "Confirmed",
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    await _prescriptionItemRepository.AddAsync(newItem);
                    
                    _logger.LogInformation("Added new PrescriptionItem: Type={Type}, Code={Code}, Name={Name}, Fee={Fee}, Status=Confirmed", 
                        itemType, itemCode, item.Name, item.Fee);
                }
                else if (existingItem.Status == "Pending" || existingItem.Status == "Confirmed")
                {
                    existingItem.ItemName = item.Name ?? existingItem.ItemName;
                    existingItem.Quantity = item.Quantity;
                    existingItem.Unit = item.Unit ?? existingItem.Unit;
                    existingItem.Price = item.Fee ?? existingItem.Price;
                    existingItem.Status = "Confirmed";
                    existingItem.UpdatedAt = DateTime.UtcNow;
                    
                    await _prescriptionItemRepository.UpdateAsync(existingItem);
                    
                    _logger.LogInformation("Updated PrescriptionItem {Id}: Quantity={Quantity}, Price={Price}, Status=Confirmed", 
                        existingItem.Id, item.Quantity, item.Fee);
                }
                else if (existingItem.Status == "CancelRequested")
                {
                    existingItem.Status = "Cancelled";
                    existingItem.UpdatedAt = DateTime.UtcNow;
                    
                    await _prescriptionItemRepository.UpdateAsync(existingItem);
                    
                    _logger.LogInformation("Cancelled PrescriptionItem {Id} (was CancelRequested)", existingItem.Id);
                }
                else
                {
                    _logger.LogWarning("Cannot update PrescriptionItem {Id} with status {Status}", 
                        existingItem.Id, existingItem.Status);
                }
            }

            await _prescriptionItemRepository.SaveChangesAsync();
            _logger.LogInformation("Successfully synced {Count} prescription items", prescriptionList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing prescription items for MedicalRecord {Id}", medicalRecordId);
        }
    }

    private MedicalRecordDto MapToDto(MedicalRecord record, decimal paidAmount)
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
            PaidAmount = paidAmount,
            PaymentStatus = record.PaymentStatus,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            Patient = record.Patient != null ? new PatientDto
            {
                Id = record.Patient.Id,
                Name = record.Patient.Name,
                Age = record.Patient.Age,
                Email = record.Patient.Email,
                Status = record.Patient.Status.ToString()
            } : null,
            Doctor = record.Doctor != null ? new DoctorDto
            {
                Id = record.Doctor.Id,
                Name = record.Doctor.Name,
                Specialty = record.Doctor.Specialty,
                Email = record.Doctor.Email
            } : null,
            Appointment = record.Appointment != null ? new SimpleAppointmentDto
            {
                Id = record.Appointment.Id,
                Date = record.Appointment.Date,
                Status = record.Appointment.Status
            } : null
        };
    }
}

public class PrescriptionItemDto
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("quantity")]
    public int? Quantity { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("unit")]
    public string? Unit { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("fee")]
    public decimal? Fee { get; set; }
}