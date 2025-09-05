using System.ComponentModel.DataAnnotations;

namespace NotificationService.API.Models
{
    public class Notification
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string Recipient { get; set; } = string.Empty; // email, phone, device token
        
        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string ChannelType { get; set; } = string.Empty; // Email, SMS, Push
        
        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
        
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? SentAt { get; set; }
        
        public int RetryCount { get; set; } = 0;
        
        public string? Metadata { get; set; } // JSON for additional data
        
        // Add AppointmentDate property to fix RabbitMQ errors
        public DateTime? AppointmentDate { get; set; }
        
        // Legacy properties for backward compatibility (can be removed later)
        public int? AppointmentId { get; set; }
        public string? PatientName { get; set; }
        public string? DoctorName { get; set; }
        public string? RecipientEmail { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        public bool IsRead { get; set; }
    }

    public enum NotificationStatus
    {
        Pending,
        Sent,
        Failed,
        Retrying,
        Cancelled
    }

    // Constants for channel types
    public static class NotificationChannels
    {
        public const string Email = "Email";
        public const string SMS = "SMS";
        public const string Push = "Push";
        
        public static readonly string[] All = { Email, SMS, Push };
        
        public static bool IsValid(string channelType)
        {
            return All.Contains(channelType, StringComparer.OrdinalIgnoreCase);
        }
    }
}