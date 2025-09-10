using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Storages;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http;
using System.Linq;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly HospitalDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ILogger<ImagesController> _logger;

        public ImagesController(HospitalDbContext context, IStorageService storageService, ILogger<ImagesController> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        public async Task<ActionResult<ImageInfo>> UploadImage([FromForm] ImageUploadRequest request)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);

                if (userContext.UserType == "Doctor" && userContext.UserId != request.DoctorId)
                    return Forbid("You can only upload images for your own appointments");

                if (request.Image == null || request.Image.Length == 0)
                    return BadRequest("No image file provided");

                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
                if (!allowedTypes.Contains(request.Image.ContentType?.ToLower()))
                    return BadRequest("Invalid file type. Only JPEG, PNG, GIF, and BMP are allowed");

                var fileExtension = Path.GetExtension(request.Image.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var objectKey = $"appointments/{request.AppointmentId}/images/{uniqueFileName}";

                // Upload via generic storage service (Seaweed)
                await using var stream = request.Image.OpenReadStream();
                var publicUrl = await _storageService.UploadAsync(stream, objectKey, request.Image.ContentType);

                var imageInfo = new ImageInfo
                {
                    AppointmentId = request.AppointmentId,
                    DoctorId = request.DoctorId,
                    PatientId = request.PatientId,
                    FileName = uniqueFileName,
                    OriginalFileName = request.Image.FileName,
                    ContentType = request.Image.ContentType,
                    FileSize = request.Image.Length,
                    Description = request.Description,
                    ImageType = request.ImageType,
                    UploadedAt = DateTime.UtcNow,
                    MinioObjectKey = Path.GetFileName(publicUrl)
                };

                _context.Images.Add(imageInfo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Image uploaded successfully for appointment {AppointmentId} by doctor {DoctorId}",
                    request.AppointmentId, userContext.UserId);

                return Ok(new { image = imageInfo, url = publicUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image for appointment {AppointmentId}", request.AppointmentId);
                return StatusCode(500, "Error uploading image");
            }
        }

        [HttpGet("download/{imageId}")]
        [Authorize(Roles = "Doctor,Patient")]
        public async Task<IActionResult> DownloadImage(int imageId)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);
                var imageInfo = await _context.Images.FindAsync(imageId);

                if (imageInfo == null)
                    return NotFound("Image not found");

                if (userContext.UserType == "Doctor" && userContext.UserId != imageInfo.DoctorId)
                    return Forbid("You can only access images you uploaded");

                if (userContext.UserType == "Patient" && userContext.UserId != imageInfo.PatientId)
                    return Forbid("You can only access your own medical images");

                var stored = imageInfo.MinioObjectKey ?? string.Empty;
                if (Uri.IsWellFormedUriString(stored, UriKind.Absolute))
                {
                    return Redirect(stored);
                }

                var stream = await _storageService.DownloadAsync(stored);
                if (stream == null)
                    return NotFound("File not found in storage");

                return File(stream, imageInfo.ContentType ?? "application/octet-stream", imageInfo.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading image {ImageId}", imageId);
                return StatusCode(500, "Error downloading image");
            }
        }

        [HttpGet("appointment/{appointmentId}")]
        [Authorize(Roles = "Doctor,Patient")]
        public async Task<ActionResult<List<ImageInfo>>> GetImagesByAppointment(int appointmentId)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);
                var query = _context.Images.Where(i => i.AppointmentId == appointmentId);

                if (userContext.UserType == "Doctor")
                    query = query.Where(i => i.DoctorId == userContext.UserId);
                else if (userContext.UserType == "Patient")
                    query = query.Where(i => i.PatientId == userContext.UserId);

                var images = await query.ToListAsync();
                return Ok(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images for appointment {AppointmentId}", appointmentId);
                return StatusCode(500, "Error retrieving images");
            }
        }

        [HttpGet("patient/{patientId}")]
        [Authorize(Roles = "Doctor,Patient")]
        public async Task<ActionResult<List<ImageInfo>>> GetImagesByPatient(int patientId)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);

                if (userContext.UserType == "Patient" && userContext.UserId != patientId)
                    return Forbid("You can only access your own medical images");

                var images = await _context.Images
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.UploadedAt)
                    .ToListAsync();

                return Ok(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images for patient {PatientId}", patientId);
                return StatusCode(500, "Error retrieving images");
            }
        }

        [HttpDelete("{imageId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);
                var imageInfo = await _context.Images.FindAsync(imageId);

                if (imageInfo == null)
                    return NotFound("Image not found");

                if (userContext.UserId != imageInfo.DoctorId)
                    return Forbid("You can only delete images you uploaded");

                // Delete from storage (pass stored URL or fid)
                await _storageService.DeleteAsync(imageInfo.MinioObjectKey);

                _context.Images.Remove(imageInfo);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image {ImageId}", imageId);
                return StatusCode(500, "Error deleting image");
            }
        }
    }
}