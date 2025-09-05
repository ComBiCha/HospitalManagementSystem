using Microsoft.AspNetCore.Mvc;
using NotificationService.API.Services;
using NotificationService.API.Services.Interfaces;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationServiceManager _notificationService;

        public NotificationsController(NotificationServiceManager notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Send a notification
        /// </summary>
        /// <param name="request">Notification details</param>
        /// <returns>Success or failure response</returns>
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            var message = new NotificationMessage
            {
                Recipient = request.Recipient,
                Subject = request.Subject,
                Content = request.Content,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            var result = await _notificationService.SendNotificationAsync(request.ChannelType, message);
            
            return result ? Ok(new { Success = true }) : BadRequest(new { Success = false });
        }

        /// <summary>
        /// Send a notification via multiple channels
        /// </summary>
        /// <param name="request">Notification details for multiple channels</param>
        /// <returns>Results of the send operation for each channel</returns>
        [HttpPost("send-multi")]
        public async Task<IActionResult> SendMultiChannelNotification([FromBody] SendMultiChannelRequest request)
        {
            var message = new NotificationMessage
            {
                Recipient = request.Recipient,
                Subject = request.Subject,
                Content = request.Content,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            var results = await _notificationService.SendMultiChannelAsync(request.ChannelTypes, message);
            
            return Ok(new { Results = results });
        }

        /// <summary>
        /// Test email sending
        /// </summary>
        /// <param name="toEmail">Recipient email address</param>
        /// <returns>Success or failure response</returns>
        [HttpPost("test-email")]
        public async Task<IActionResult> TestEmail([FromBody] string toEmail)
        {
            try
            {
                var message = new NotificationMessage
                {
                    Recipient = toEmail,
                    Subject = "Test Email from HMS",
                    Content = "This is a test email from Hospital Management System"
                };

                var result = await _notificationService.SendNotificationAsync("Email", message);
                return Ok(new { Success = result, Message = "Email test completed" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }
    }

    public class SendNotificationRequest
    {
        public required string Recipient { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public required string ChannelType { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class SendMultiChannelRequest
    {
        public required string Recipient { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public required List<string> ChannelTypes { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
