using NotificationService.API.Services.Interfaces;

namespace NotificationService.API.Services.Channels
{
    public class EmailNotificationChannel : INotificationChannel
    {
        public string ChannelType => "Email";

        public async Task<bool> SendAsync(NotificationMessage message)
        {
            // TODO: Implement email sending logic (SMTP, SendGrid, etc.)
            await Task.Delay(100); // Simulate async operation
            Console.WriteLine($"Email sent to {message.Recipient}: {message.Subject}");
            return true;
        }

        public bool IsAvailable() => true;
    }
}