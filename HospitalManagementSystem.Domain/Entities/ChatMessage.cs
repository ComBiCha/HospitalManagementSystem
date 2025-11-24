using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        public int ChatRoomId { get; set; }

        [Required]
        public int SenderId { get; set; } // UserId from the User entity

        public string? Content { get; set; } // Text content, nullable if only image
        public string? ImageUrl { get; set; } // Image URL, nullable if only text

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string MessageType { get; set; } = "text"; // e.g., "text", "image", "text_image", "system"

        // New fields for "received" and "seen" status for Patient and Doctor
        public bool IsReceivedByPatient { get; set; } = false;
        public bool IsSeenByPatient { get; set; } = false;
        public bool IsReceivedByDoctor { get; set; } = false;
        public bool IsSeenByDoctor { get; set; } = false;

        // Navigation properties
        public ChatRoom ChatRoom { get; set; } = null!;
        public User Sender { get; set; } = null!;
    }
}