
using System;
using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Application.DTOs
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int ChatRoomId { get; set; }
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime SentAt { get; set; }
        public string MessageType { get; set; } = "text";
        public bool IsReceivedByPatient { get; set; }
        public bool IsSeenByPatient { get; set; }
        public bool IsReceivedByDoctor { get; set; }
        public bool IsSeenByDoctor { get; set; }
        public UserInfoDto Sender { get; set; } = null!;

        public static ChatMessageDto FromChatMessage(ChatMessage message)
        {
            return new ChatMessageDto
            {
                Id = message.Id,
                ChatRoomId = message.ChatRoomId,
                SenderId = message.SenderId,
                Content = message.Content,
                ImageUrl = message.ImageUrl,
                SentAt = message.SentAt,
                MessageType = message.MessageType,
                IsReceivedByPatient = message.IsReceivedByPatient,
                IsSeenByPatient = message.IsSeenByPatient,
                IsReceivedByDoctor = message.IsReceivedByDoctor,
                IsSeenByDoctor = message.IsSeenByDoctor,
                Sender = new UserInfoDto
                {
                    Id = message.Sender.Id,
                    FirstName = message.Sender.FirstName,
                    LastName = message.Sender.LastName,
                    Role = message.Sender.Role
                }
            };
        }
    }

    public class UserInfoDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
