using HospitalManagementSystem.Domain.Entities;

namespace HospitalManagementSystem.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int id);
    Task<List<Notification>> GetUserNotificationsAsync(int userId, int limit = 50);
    Task<List<Notification>> GetUnreadNotificationsAsync(int userId);
    Task<Notification> CreateAsync(Notification notification);
    Task<Notification> UpdateAsync(Notification notification);
    Task MarkAsReadAsync(int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task DeleteAsync(int id);
    Task<int?> GetUserIdFromDoctorIdAsync(int doctorId);
}
