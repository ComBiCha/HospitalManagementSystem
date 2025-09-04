using ImageService.API.Data;
using ImageService.API.Models;
using ImageService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ImageService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly ImageDbContext _context;
        private readonly IMinioService _minioService;
        private readonly ILogger<ImagesController> _logger;

        public ImagesController(ImageDbContext context, IMinioService minioService, ILogger<ImagesController> logger)
        {
            _context = context;
            _minioService = minioService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "Doctor")]
        public async Task<ActionResult<ImageInfo>> UploadImage([FromForm] ImageUploadRequest request)
        {
            try
            {
                var userContext = UserContext.FromClaims(User);
                
                // Verify that the doctor can only upload for their own appointments
                if (userContext.UserType == "Doctor" && userContext.UserId != request.DoctorId)
                {
                    return Forbid("You can only upload images for your own appointments");
                }

                // Validate file
                if (request.Image == null || request.Image.Length == 0)
                    return BadRequest("No image file provided");

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
                if (!allowedTypes.Contains(request.Image.ContentType.ToLower()))
                    return BadRequest("Invalid file type. Only JPEG, PNG, GIF, and BMP are allowed");

                // Generate unique filename
                var fileExtension = Path.GetExtension(request.Image.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var objectKey = $"appointments/{request.AppointmentId}/images/{uniqueFileName}";

                // Upload to MinIO
                await _minioService.UploadImageAsync(request.Image, objectKey);

                // Save to database
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
                    MinioObjectKey = objectKey
                };

                _context.Images.Add(imageInfo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Image uploaded successfully for appointment {AppointmentId} by doctor {DoctorId}", 
                    request.AppointmentId, userContext.UserId);
                return Ok(imageInfo);
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

                // Authorization check: Doctors can access their uploaded images, Patients can access their own images
                if (userContext.UserType == "Doctor" && userContext.UserId != imageInfo.DoctorId)
                {
                    return Forbid("You can only access images you uploaded");
                }
                
                if (userContext.UserType == "Patient" && userContext.UserId != imageInfo.PatientId)
                {
                    return Forbid("You can only access your own medical images");
                }

                var imageResponse = await _minioService.DownloadImageAsync(
                    imageInfo.MinioObjectKey, 
                    imageInfo.OriginalFileName, 
                    imageInfo.ContentType);

                return File(imageResponse.ImageStream, imageResponse.ContentType, imageResponse.FileName);
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
                
                // Authorization check
                if (userContext.UserType == "Doctor")
                {
                    query = query.Where(i => i.DoctorId == userContext.UserId);
                }
                else if (userContext.UserType == "Patient")
                {
                    query = query.Where(i => i.PatientId == userContext.UserId);
                }

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
                
                // Authorization check: Patients can only see their own images
                if (userContext.UserType == "Patient" && userContext.UserId != patientId)
                {
                    return Forbid("You can only access your own medical images");
                }

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

                // Only the doctor who uploaded can delete
                if (userContext.UserId != imageInfo.DoctorId)
                {
                    return Forbid("You can only delete images you uploaded");
                }

                // Delete from MinIO
                await _minioService.DeleteImageAsync(imageInfo.MinioObjectKey);

                // Delete from database
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
