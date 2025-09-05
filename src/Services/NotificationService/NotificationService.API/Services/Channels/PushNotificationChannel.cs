using NotificationService.API.Services.Interfaces;

namespace NotificationService.API.Services.Channels
{
    public class PushNotificationChannel : INotificationChannel
    {
        public string ChannelType => "Push";

        public async Task<bool> SendAsync(NotificationMessage message)
        {
            // TODO: Implement push notification logic (Firebase, OneSignal, etc.)
            await Task.Delay(100); // Simulate async operation
            Console.WriteLine($"Push notification sent to {message.Recipient}: {message.Content}");
            return true;
        }

        public bool IsAvailable() => true;
    }
}