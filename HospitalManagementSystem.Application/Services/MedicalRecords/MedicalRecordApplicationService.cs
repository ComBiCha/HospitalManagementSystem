using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs.MedicalRecord;
using HospitalManagementSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using HospitalManagementSystem.Domain.Caching;
using HospitalManagementSystem.Domain.Specifications;
using HospitalManagementSystem.Application.Services;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Application.DTOs.Common;
using HospitalManagementSystem.Application.DTOs;

public class MedicalRecordApplicationService
{
    private readonly ILogger<MedicalRecordApplicationService> _logger;
    private readonly IMedicalRecordRepository _medicalRecordRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPrescriptionItemRepository _prescriptionItemRepository;
    private readonly IMedicalRecordHistoryRepository _medicalRecordHistoryRepository;
    private readonly ICacheService _cacheService;

    public MedicalRecordApplicationService(
        IMedicalRecordRepository medicalRecordRepository,
        IAppointmentRepository appointmentRepository,
        IPrescriptionItemRepository prescriptionItemRepository,
        IMedicalRecordHistoryRepository medicalRecordHistoryRepository,
        ILogger<MedicalRecordApplicationService> logger,
        ICacheService cacheService)
    {
        _medicalRecordRepository = medicalRecordRepository;
        _appointmentRepository = appointmentRepository;
        _prescriptionItemRepository = prescriptionItemRepository;
        _medicalRecordHistoryRepository = medicalRecordHistoryRepository;
        _logger = logger;
        _cacheService = cacheService;
    }
    public async Task<MedicalRecordDto?> GetMedicalRecordByAppointmentAsync(int appointmentId, int doctorId)
    {
        var medicalRecord = await _medicalRecordRepository.GetByAppointmentIdAsync(appointmentId);
        if (medicalRecord == null || medicalRecord.DoctorId != doctorId)
            return null;

        return MapToDto(medicalRecord);
    }

    public async Task<MedicalRecordDto?> GetMedicalRecordByIdAsync(int id)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null) return null;

        return MapToDto(medicalRecord);
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

        await _cacheService.RemovePatternAsync($"patient:{medicalRecord.PatientId}:medical-records:*");
        _logger.LogInformation("Cleared medical record cache for patient {PatientId}", medicalRecord.PatientId);

        return ("Cập nhật thành công", MapToDto(medicalRecord));
    }

    public async Task<(string Message, bool Success)> CompleteMedicalRecordAsync(int id)
    {
        var medicalRecord = await _medicalRecordRepository.GetByIdAsync(id);
        if (medicalRecord == null)
            return ("Medical record not found", false);

        var prescriptionItems = await _prescriptionItemRepository.GetByMedicalRecordIdAsync(id);

        var nonFinalItems = prescriptionItems.Where(item => {
            // Final states that are universally acceptable.
            if (item.Status == "Cancelled") return false;

            if (item.ItemType == "Test")
            {
                // For a test, the only other acceptable final state is "Completed".
                return item.Status != "Completed";
            }
            else // Assuming "Medicine"
            {
                // For a medicine, "Confirmed" is sufficient. "Completed" is also a valid final state.
                return item.Status != "Confirmed" && item.Status != "Completed";
            }
        }).ToList();

        if (nonFinalItems.Any())
        {
            var firstError = nonFinalItems.First();
            var requiredState = firstError.ItemType == "Test" ? "'Completed'" : "'Confirmed' or 'Completed'";
            var message = $"Không thể hoàn thành. {firstError.ItemType} '{firstError.ItemName}' phải ở trạng thái {requiredState} hoặc 'Cancelled'. Trạng thái hiện tại: '{firstError.Status}'.";
            return (message, false);
        }

        if (medicalRecord.Appointment != null)
        {
            medicalRecord.Appointment.Status = "Completed";
            medicalRecord.Appointment.UpdatedAt = DateTime.UtcNow;
            await _appointmentRepository.UpdateAsync(medicalRecord.Appointment);
        }

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

        await _cacheService.RemovePatternAsync($"patient:{medicalRecord.PatientId}:medical-records:*");
        _logger.LogInformation("Cleared medical record cache for patient {PatientId}", medicalRecord.PatientId);

        return ("Hoàn thành khám bệnh", true);
    }

    public async Task<PagedResult<PatientMedicalRecordDto>> GetMedicalRecordsForPatientAsync(int patientId, MedicalRecordFilterDto filter, int page, int pageSize)
    {
        var filterCacheKey = $"spec:{filter.Specialty ?? "any"}_doc:{filter.DoctorName ?? "any"}_start:{filter.StartDate?.Ticks ?? 0}_end:{filter.EndDate?.Ticks ?? 0}";
        var cacheKey = $"patient:{patientId}:medical-records:page:{page}:{pageSize}:{filterCacheKey}";
        var cachedResult = await _cacheService.GetAsync<PagedResult<PatientMedicalRecordDto>>(cacheKey);

        if (cachedResult != null)
        {
            _logger.LogInformation("Cache HIT for key: {CacheKey}", cacheKey);
            return cachedResult;
        }

        _logger.LogInformation("Cache MISS for key: {CacheKey}", cacheKey);

        ISpecification<MedicalRecord> spec = new MedicalRecordForPatientSpecification(patientId);

        if (!string.IsNullOrEmpty(filter.Specialty))
        {
            spec = spec.And(new MedicalRecordBySpecialtySpecification(filter.Specialty));
        }

        if (!string.IsNullOrEmpty(filter.DoctorName))
        {
            spec = spec.And(new MedicalRecordByDoctorNameSpecification(filter.DoctorName));
        }

        if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
            var endDateValue = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            var endDateUtc = DateTime.SpecifyKind(endDateValue, DateTimeKind.Utc);
            spec = spec.And(new MedicalRecordByDateRangeSpecification(startDateUtc, endDateUtc));
        }

        var query = _medicalRecordRepository.GetQueryable(spec);
        var totalCount = await query.CountAsync();

        var records = await query
            .OrderByDescending(m => m.Appointment.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = records.Select(m => new PatientMedicalRecordDto
        {
            Id = m.Id,
            AppointmentDate = m.Appointment.Date,
            DoctorName = m.Doctor.Name,
            DoctorSpecialty = m.Doctor.Specialty,
            Diagnosis = m.Diagnosis, 
            Symptoms = m.Symptoms,
            Treatment = m.Treatment,
            Prescription = m.Prescription,
            Notes = m.Notes,
            ConsultationFee = m.ConsultationFee,
            MedicineFee = m.MedicineFee,
            TestFee = m.TestFee,
            OtherFee = m.OtherFee,
            PaidAmount = m.PaidAmount,
            PaymentStatus = m.PaymentStatus,
            AppointmentStatus = m.Appointment.Status
        }).ToList();

        var pagedResult = new PagedResult<PatientMedicalRecordDto>(dtos, page, pageSize, totalCount);

        await _cacheService.SetAsync(cacheKey, pagedResult, TimeSpan.FromHours(1));

        return pagedResult;
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

        await _cacheService.RemovePatternAsync($"patient:{medicalRecord.PatientId}:medical-records:*");
        _logger.LogInformation("Cleared medical record cache for patient {PatientId}", medicalRecord.PatientId);

        return ("Đã chuyển nhập viện", MapToDto(medicalRecord));
    }

    public async Task<(bool Success, string Message)> CompletePrescriptionItemAsync(int id)
    {
        var item = await _prescriptionItemRepository.GetByIdAsync(id);
        if (item == null) return (false, "Item not found");

        if (item.ItemType != "Test")
        {
            return (false, "Only test items can be completed.");
        }

        if (item.Status != "Confirmed")
        {
            return (false, $"Cannot complete test with status: {item.Status}");
        }

        item.Status = "Completed";
        item.UpdatedAt = DateTime.UtcNow;

        await _prescriptionItemRepository.UpdateAsync(item);
        await _prescriptionItemRepository.SaveChangesAsync();

        await InvalidatePatientMedicalRecordCache(item.MedicalRecordId);
        
        _logger.LogInformation("Prescription item {Id} marked as completed.", id);
        return (true, "Xét nghiệm đã được hoàn thành");
    }

    public async Task<(bool Success, string Message)> RequestCancelPrescriptionItemAsync(int id, int doctorId, string reason)
    {
        var item = await _prescriptionItemRepository.GetByIdAsync(id);
        if (item == null) return (false, "Item not found");

        if (item.Status == "Pending")
        {
            await _prescriptionItemRepository.DeleteAsync(item);
        }
        else
        {
            item.IsCancelRequested = true;
            item.CancelReason = reason;
            item.Status = "CancelRequested";
            item.CancelRequestedAt = DateTime.UtcNow;
            item.CancelRequestedByDoctorId = doctorId;
            item.UpdatedAt = DateTime.UtcNow;
            await _prescriptionItemRepository.UpdateAsync(item);
        }

        await _prescriptionItemRepository.SaveChangesAsync();
        await InvalidatePatientMedicalRecordCache(item.MedicalRecordId);
        return (true, "Đã yêu cầu hủy");
    }

    public async Task<(bool Success, string Message)> ApproveCancelPrescriptionItemAsync(int id)
    {
        var item = await _prescriptionItemRepository.GetByIdAsync(id);
        if (item == null) return (false, "Item not found");

        item.Status = "Cancelled";
        item.UpdatedAt = DateTime.UtcNow;

        await _prescriptionItemRepository.UpdateAsync(item);
        await _prescriptionItemRepository.SaveChangesAsync();

        await InvalidatePatientMedicalRecordCache(item.MedicalRecordId);
        return (true, "Đã duyệt hủy");
    }

    private async Task InvalidatePatientMedicalRecordCache(int medicalRecordId)
    {
        var record = await _medicalRecordRepository.GetByIdAsync(medicalRecordId);
        if (record != null)
        {
            await _cacheService.RemovePatternAsync($"patient:{record.PatientId}:medical-records:*");
            _logger.LogInformation("Cleared medical record cache for patient {PatientId}", record.PatientId);
        }
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
            if (prescriptionList == null)
            {
                _logger.LogWarning("Prescription list is null, cannot sync.");
                return;
            }

            foreach (var itemDto in prescriptionList)
            {
                var itemCode = (itemDto.Code ?? itemDto.Name ?? "").Trim();
                if (string.IsNullOrEmpty(itemCode))
                {
                    _logger.LogWarning("Skipping item with empty code: {Item}", JsonSerializer.Serialize(itemDto));
                    continue;
                }

                // Case 1: The item from the frontend has an ID. It's an existing item.
                if (itemDto.Id.HasValue && itemDto.Id > 0)
                {
                    var dbItem = await _prescriptionItemRepository.GetByIdAsync(itemDto.Id.Value);
                    if (dbItem != null)
                    {
                        // Only update items that are not in a terminal state.
                        if (dbItem.Status == "Pending" || dbItem.Status == "Confirmed")
                        {
                            dbItem.Quantity = itemDto.Quantity;
                            dbItem.ItemName = itemDto.Name ?? dbItem.ItemName;
                            dbItem.Unit = itemDto.Unit ?? dbItem.Unit;
                            dbItem.Price = itemDto.Fee ?? dbItem.Price;
                            dbItem.UpdatedAt = DateTime.UtcNow;
                            await _prescriptionItemRepository.UpdateAsync(dbItem);
                            _logger.LogInformation("Updated existing PrescriptionItem {Id}", dbItem.Id);
                        }
                        else if (dbItem.Status == "CancelRequested")
                        {
                            dbItem.Status = "Cancelled";
                            dbItem.UpdatedAt = DateTime.UtcNow;
                            await _prescriptionItemRepository.UpdateAsync(dbItem);
                            _logger.LogInformation("Cancelled PrescriptionItem {Id} via draft save.", dbItem.Id);
                        }
                        // If status is already Cancelled or Completed, we do nothing to the existing record.
                    }
                }
                // Case 2: The item from the frontend has NO ID. It's a new item.
                else
                {
                    // Before creating, we must check if an ACTIVE item with the same code already exists.
                    var activeExistingItem = await _prescriptionItemRepository.GetActiveByMedicalRecordAndCodeAsync(medicalRecordId, itemCode);
                    if (activeExistingItem == null)
                    {
                        // No active item exists, so we can create this new one.
                        var newItem = new PrescriptionItem
                        {
                            MedicalRecordId = medicalRecordId,
                            ItemType = itemDto.Type?.ToLower() == "drug" ? "Medicine" : "Test",
                            ItemCode = itemCode,
                            ItemName = itemDto.Name ?? "",
                            Quantity = itemDto.Quantity,
                            Unit = itemDto.Unit ?? (itemDto.Type?.ToLower() == "drug" ? "viên" : "lần"),
                            Price = itemDto.Fee ?? 0,
                            Status = "Confirmed", // New items are confirmed by default when saved.
                            CreatedAt = DateTime.UtcNow
                        };
                        await _prescriptionItemRepository.AddAsync(newItem);
                        _logger.LogInformation("Added new PrescriptionItem: Code={Code}", itemCode);
                    }
                    // If activeExistingItem is NOT null, we do nothing to prevent creating a duplicate active item.
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
            UpdatedAt = record.UpdatedAt,
            Patient = record.Patient != null ? new PatientDto
            {
                Id = record.Patient.Id,
                Name = record.Patient.Name,
                Age = record.Patient.Age,
                Email = record.Patient.Email,
                Status = record.Patient.Status.ToString(),
                PatientIdentifiers = record.Patient.PatientIdentifiers?.Select(pi => new PatientIdentifierDto
                {
                    EHRSystem = pi.EHRSystem,
                    ExternalId = pi.ExternalId,
                    IdentifierType = pi.IdentifierType
                }).ToList()
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
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public int? Id { get; set; }

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