using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Domain.Dicom;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace HospitalManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DicomController : ControllerBase
    {
        private readonly IDicomService _dicomService;
        private readonly HospitalDbContext _context;

        public DicomController(IDicomService dicomService, HospitalDbContext context)
        {
            _dicomService = dicomService;
            _context = context;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        [ProducesResponseType(typeof(ImageInfoDto), 200)]
        public async Task<IActionResult> UploadDicomImage([FromForm] DicomUploadRequestDto request)
        {
            var medicalRecord = await _context.MedicalRecords.FindAsync(request.MedicalRecordId);
            if (medicalRecord == null)
            {
                return NotFound("Medical record not found.");
            }

            if (!await CanUserAccessMedicalRecord(medicalRecord))
            {
                return Forbid();
            }

            var imageInfoEntity = await _dicomService.UploadDicomImageAsync(request.MedicalRecordId, request.File, request.Description, request.ImageType);
            return Ok(MapToDto(imageInfoEntity));
        }

        [HttpGet("{imageId}")]
        [ProducesResponseType(typeof(ImageInfoDto), 200)]
        public async Task<IActionResult> GetImageInfo(int imageId)
        {
            var imageInfoEntity = await _dicomService.GetImageInfoAsync(imageId);
            if (imageInfoEntity == null)
            {
                return NotFound();
            }

            var medicalRecord = await _context.MedicalRecords.FindAsync(imageInfoEntity.MedicalRecordId);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            if (!await CanUserAccessMedicalRecord(medicalRecord))
            {
                return Forbid();
            }

            return Ok(MapToDto(imageInfoEntity));
        }

        [HttpGet("medical-record/{medicalRecordId}")]
        [ProducesResponseType(typeof(IEnumerable<ImageInfoDto>), 200)]
        public async Task<IActionResult> GetImagesForMedicalRecord(int medicalRecordId)
        {
            var medicalRecord = await _context.MedicalRecords.FindAsync(medicalRecordId);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            if (!await CanUserAccessMedicalRecord(medicalRecord))
            {
                return Forbid();
            }

            var imageInfoEntities = await _dicomService.GetImagesForMedicalRecordAsync(medicalRecordId);
            return Ok(imageInfoEntities.Select(MapToDto));
        }

        [HttpGet("{imageId}/download")]
        public async Task<IActionResult> DownloadImage(int imageId)
        {
            // We get the entity here for the auth check, but the service method for download remains the same.
            var imageInfoEntity = await _dicomService.GetImageInfoAsync(imageId);
            if (imageInfoEntity == null)
            {
                return NotFound();
            }

            var medicalRecord = await _context.MedicalRecords.FindAsync(imageInfoEntity.MedicalRecordId);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            if (!await CanUserAccessMedicalRecord(medicalRecord))
            {
                return Forbid();
            }

            var response = await _dicomService.DownloadImageAsync(imageId);
            if (response.ImageStream == null)
            {
                return StatusCode(500, "Error retrieving file from storage.");
            }
            return File(response.ImageStream, response.ContentType, response.FileName);
        }

        [HttpGet("{imageId}/preview")]
        public async Task<IActionResult> GetImagePreview(int imageId)
        {
            var imageInfoEntity = await _dicomService.GetImageInfoAsync(imageId);
            if (imageInfoEntity == null)
            {
                return NotFound();
            }

            var medicalRecord = await _context.MedicalRecords.FindAsync(imageInfoEntity.MedicalRecordId);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            if (!await CanUserAccessMedicalRecord(medicalRecord))
            {
                return Forbid();
            }

            var preview = await _dicomService.GetImagePreviewAsync(imageId);
            return File(preview, "image/jpeg");
        }

        // --- Mapping Helper ---
        private ImageInfoDto MapToDto(ImageInfo entity)
        {
            return new ImageInfoDto
            {
                Id = entity.Id,
                MedicalRecordId = entity.MedicalRecordId,
                FileName = entity.FileName,
                OriginalFileName = entity.OriginalFileName,
                ContentType = entity.ContentType,
                FileSize = entity.FileSize,
                Description = entity.Description,
                ImageType = entity.ImageType,
                UploadedAt = entity.UploadedAt,
                MinioObjectKey = entity.MinioObjectKey,
                OrthancInstanceId = entity.OrthancInstanceId
            };
        }

        // --- Authorization Helper Methods ---

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private int? GetCurrentUserPatientId()
        {
            var patientIdClaim = User.FindFirst("PatientId")?.Value;
            return int.TryParse(patientIdClaim, out int patientId) ? patientId : null;
        }

        private int? GetCurrentUserDoctorId()
        {
            var doctorIdClaim = User.FindFirst("DoctorId")?.Value;
            return int.TryParse(doctorIdClaim, out int doctorId) ? doctorId : null;
        }

        private async Task<bool> CanUserAccessMedicalRecord(MedicalRecord medicalRecord)
        {
            var userRole = GetCurrentUserRole();
            await Task.CompletedTask; // To make the method async as intended for future use

            switch (userRole)
            {
                case "Admin":
                    return true; // Admin can access everything

                case "Doctor":
                    var userDoctorId = GetCurrentUserDoctorId();
                    return userDoctorId.HasValue && userDoctorId.Value == medicalRecord.DoctorId;

                case "Patient":
                    var userPatientId = GetCurrentUserPatientId();
                    return userPatientId.HasValue && userPatientId.Value == medicalRecord.PatientId;

                default:
                    return false;
            }
        }
    }
}
