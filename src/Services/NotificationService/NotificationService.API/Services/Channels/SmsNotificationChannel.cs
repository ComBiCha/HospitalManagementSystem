using NotificationService.API.Services.Interfaces;

namespace NotificationService.API.Services.Channels
{
    public class SmsNotificationChannel : INotificationChannel
    {
        public string ChannelType => "SMS";

        public async Task<bool> SendAsync(NotificationMessage message)
        {
            // TODO: Implement SMS sending logic (Twilio, AWS SNS, etc.)
            await Task.Delay(100); // Simulate async operation
            Console.WriteLine($"SMS sent to {message.Recipient}: {message.Content}");
            return true;
        }

        public bool IsAvailable() => true;
    }
}