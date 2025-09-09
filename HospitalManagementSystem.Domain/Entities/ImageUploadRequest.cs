using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HospitalManagementSystem.Domain.Entities
{
    public class ImageUploadRequest
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public IFormFile Image { get; set; } = null!;
        public string? Description { get; set; }
        public string ImageType { get; set; } = "medical"; // medical, xray, scan, etc.
    }

    public class ImageInfo
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Description { get; set; }
        public string ImageType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string MinioObjectKey { get; set; } = string.Empty;
    }

    public class ImageDownloadResponse
    {
        public Stream ImageStream { get; set; } = null!;
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class UserContext
    {
        public int UserId { get; set; }
        public string UserType { get; set; } = string.Empty; // "Doctor" or "Patient"
        public string Email { get; set; } = string.Empty;

        public static UserContext FromClaims(ClaimsPrincipal user)
        {
            return new UserContext
            {
                UserId = int.Parse(user.FindFirst("user_id")?.Value ?? "0"),
                UserType = user.FindFirst("user_type")?.Value ?? "",
                Email = user.FindFirst(ClaimTypes.Email)?.Value ?? ""
            };
        }
    }

    public class AuthorizedImageUploadRequest : ImageUploadRequest
    {
        public UserContext User { get; set; } = null!;
    }
}
