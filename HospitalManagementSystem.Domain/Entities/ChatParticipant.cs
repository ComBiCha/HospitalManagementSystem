using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    public class ChatParticipant
    {
        public int Id { get; set; }

        [Required]
        public int ChatRoomId { get; set; }

        [Required]
        public int UserId {
            get; set;
        }

        [Required]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LeftAt { get; set; }

        // Navigation properties
        public ChatRoom ChatRoom { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}