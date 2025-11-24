using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalManagementSystem.Domain.Entities
{
    public class ChatRoom
    {
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Appointment Appointment { get; set; } = null!;
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    }
}