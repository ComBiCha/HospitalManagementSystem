
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Application.DTOs
{
    public class DicomUploadRequestDto
    {
        [Required]
        public int MedicalRecordId { get; set; }

        [Required]
        public IFormFile File { get; set; }

        public string? Description { get; set; }

        [Required]
        public string ImageType { get; set; }
    }
}
