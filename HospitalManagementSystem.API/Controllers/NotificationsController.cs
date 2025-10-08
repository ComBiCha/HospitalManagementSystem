using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Domain.Notifications;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Caching;
using HospitalManagementSystem.Infrastructure.Persistence;
using HospitalManagementSystem.Application.Services;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationServiceManager _notificationService;
        private readonly INotificationRepository _notificationRepository;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(NotificationServiceManager notificationService, INotificationRepository notificationRepository, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _notificationRepository = notificationRepository;
            _logger = logger;
        }


        // GET: api/Notifications
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int limit = 50)
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                int userId;
                
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
                {
                    // If no UserId, try to get from DoctorId
                    var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                    if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out int doctorId))
                    {
                        var userIdFromDoctor = await _notificationRepository.GetUserIdFromDoctorIdAsync(doctorId);
                        if (userIdFromDoctor == null)
                        {
                            return BadRequest(new { message = "User ID not found for doctor" });
                        }
                        userId = userIdFromDoctor.Value;
                    }
                    else
                    {
                        return BadRequest(new { message = "User ID not found in token" });
                    }
                }

                var notifications = await _notificationRepository.GetUserNotificationsAsync(userId, limit);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notifications");
                return StatusCode(500, new { message = "Lỗi khi tải thông báo" });
            }
        }

        // GET: api/Notifications/unread
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                int userId;
                
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
                {
                    // If no UserId, try to get from DoctorId
                    var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                    if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out int doctorId))
                    {
                        var userIdFromDoctor = await _notificationRepository.GetUserIdFromDoctorIdAsync(doctorId);
                        if (userIdFromDoctor == null)
                        {
                            return BadRequest(new { message = "User ID not found for doctor" });
                        }
                        userId = userIdFromDoctor.Value;
                    }
                    else
                    {
                        return BadRequest(new { message = "User ID not found in token" });
                    }
                }

                var notifications = await _notificationRepository.GetUnreadNotificationsAsync(userId);
                return Ok(new 
                { 
                    count = notifications.Count,
                    notifications 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unread notifications");
                return StatusCode(500, new { message = "Lỗi khi tải thông báo" });
            }
        }

        // PUT: api/Notifications/{id}/mark-read
        [HttpPut("{id}/mark-read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                await _notificationRepository.MarkAsReadAsync(id);
                return Ok(new { message = "Đã đánh dấu đã đọc" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return StatusCode(500, new { message = "Lỗi khi cập nhật" });
            }
        }

        // PUT: api/Notifications/mark-all-read
        [HttpPut("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                int userId;
                
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
                {
                    // If no UserId, try to get from DoctorId
                    var doctorIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DoctorId")?.Value;
                    if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out int doctorId))
                    {
                        var userIdFromDoctor = await _notificationRepository.GetUserIdFromDoctorIdAsync(doctorId);
                        if (userIdFromDoctor == null)
                        {
                            return BadRequest(new { message = "User ID not found for doctor" });
                        }
                        userId = userIdFromDoctor.Value;
                    }
                    else
                    {
                        return BadRequest(new { message = "User ID not found in token" });
                    }
                }

                await _notificationRepository.MarkAllAsReadAsync(userId);
                return Ok(new { message = "Đã đánh dấu tất cả đã đọc" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, new { message = "Lỗi khi cập nhật" });
            }
        }

        // DELETE: api/Notifications/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                await _notificationRepository.DeleteAsync(id);
                return Ok(new { message = "Đã xóa thông báo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification");
                return StatusCode(500, new { message = "Lỗi khi xóa" });
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
