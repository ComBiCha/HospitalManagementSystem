namespace HospitalManagementSystem.Domain.Repositories
{
    public interface INotificationChannel
    {
        string ChannelType { get; }
        Task<bool> SendAsync(NotificationMessage message);
        bool IsAvailable();
    }

    public class NotificationMessage
    {
        public required string Recipient { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}