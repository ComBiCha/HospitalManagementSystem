using HospitalManagementSystem.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IChatRoomRepository
    {
        Task<ChatRoom?> GetByIdAsync(int id);
        Task<ChatRoom?> GetByAppointmentIdAsync(int appointmentId);
        Task<ChatRoom> CreateAsync(ChatRoom chatRoom);
        Task<ChatRoom?> UpdateAsync(ChatRoom chatRoom);
        Task<bool> DeleteAsync(int id);
    }
}