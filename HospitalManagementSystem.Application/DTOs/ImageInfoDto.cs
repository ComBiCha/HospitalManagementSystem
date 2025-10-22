namespace HospitalManagementSystem.Application.DTOs
{
    public class ImageInfoDto
    {
        public int Id { get; set; }
        public int MedicalRecordId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Description { get; set; }
        public string ImageType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string MinioObjectKey { get; set; } = string.Empty;
        public string? OrthancInstanceId { get; set; }
    }
}
