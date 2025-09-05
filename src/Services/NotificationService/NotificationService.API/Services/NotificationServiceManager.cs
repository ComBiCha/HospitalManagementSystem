using NotificationService.API.Services.Interfaces;

namespace NotificationService.API.Services
{
    public class NotificationServiceManager
    {
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly ILogger<NotificationServiceManager> _logger;

        public NotificationServiceManager(
            IEnumerable<INotificationChannel> channels,
            ILogger<NotificationServiceManager> logger)
        {
            _channels = channels;
            _logger = logger;
        }

        public async Task<bool> SendNotificationAsync(
            string channelType, 
            NotificationMessage message)
        {
            var channel = _channels.FirstOrDefault(c => 
                c.ChannelType.Equals(channelType, StringComparison.OrdinalIgnoreCase));

            if (channel == null)
            {
                _logger.LogWarning($"Channel {channelType} not found");
                return false;
            }

            if (!channel.IsAvailable())
            {
                _logger.LogWarning($"Channel {channelType} is not available");
                return false;
            }

            try
            {
                return await channel.SendAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send notification via {channelType}");
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> SendMultiChannelAsync(
            List<string> channelTypes, 
            NotificationMessage message)
        {
            var results = new Dictionary<string, bool>();
            
            var tasks = channelTypes.Select(async channelType =>
            {
                var result = await SendNotificationAsync(channelType, message);
                return new { ChannelType = channelType, Success = result };
            });

            var completedTasks = await Task.WhenAll(tasks);
            
            foreach (var task in completedTasks)
            {
                results[task.ChannelType] = task.Success;
            }

            return results;
        }
    }
}