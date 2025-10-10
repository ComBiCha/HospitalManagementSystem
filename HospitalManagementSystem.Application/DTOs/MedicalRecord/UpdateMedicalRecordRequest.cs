namespace HospitalManagementSystem.Application.DTOs.MedicalRecord
{
    public class UpdateMedicalRecordRequest
    {
        public string? Diagnosis { get; set; }
        public string? Symptoms { get; set; }
        public string? Treatment { get; set; }
        public string? Prescription { get; set; }
        public string? Notes { get; set; }
        public decimal? MedicineFee { get; set; }
        public decimal? TestFee { get; set; }
        public decimal? OtherFee { get; set; }
    }
}