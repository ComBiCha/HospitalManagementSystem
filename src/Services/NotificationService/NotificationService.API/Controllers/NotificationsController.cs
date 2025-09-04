using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.API.Data;
using NotificationService.API.Models;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(NotificationDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all notifications
        /// </summary>
        /// <returns>List of notifications</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            try
            {
                _logger.LogInformation("Getting all notifications via REST API");
                var notifications = await _context.Notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all notifications");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get notification by ID
        /// </summary>
        /// <param name="id">Notification ID</param>
        /// <returns>Notification details</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetNotification(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound($"Notification with ID {id} not found");
                }

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification with ID: {NotificationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get notifications by appointment ID
        /// </summary>
        /// <param name="appointmentId">Appointment ID</param>
        /// <returns>List of notifications for the appointment</returns>
        [HttpGet("appointment/{appointmentId}")]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotificationsByAppointment(int appointmentId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AppointmentId == appointmentId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for appointment: {AppointmentId}", appointmentId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        /// <param name="id">Notification ID</param>
        /// <returns>Updated notification</returns>
        [HttpPut("{id}/read")]
        public async Task<ActionResult<Notification>> MarkAsRead(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound($"Notification with ID {id} not found");
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read: {NotificationId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get unread notifications count
        /// </summary>
        /// <returns>Count of unread notifications</returns>
        [HttpGet("unread/count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            try
            {
                var count = await _context.Notifications.CountAsync(n => !n.IsRead);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications count");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
