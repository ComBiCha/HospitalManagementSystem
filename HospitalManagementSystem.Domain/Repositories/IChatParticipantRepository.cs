using HospitalManagementSystem.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IChatParticipantRepository
    {
        Task<ChatParticipant?> GetByIdAsync(int id);
        Task<IEnumerable<ChatParticipant>> GetByChatRoomIdAsync(int chatRoomId);
        Task<ChatParticipant?> GetByChatRoomAndUserIdAsync(int chatRoomId, int userId);
        Task<IEnumerable<ChatRoom>> GetChatRoomsByUserIdAsync(int userId);
        Task<bool> IsUserInChatRoomAsync(int chatRoomId, int userId);
        Task<ChatParticipant> CreateAsync(ChatParticipant chatParticipant);
        Task<ChatParticipant?> UpdateAsync(ChatParticipant chatParticipant);
        Task<bool> DeleteAsync(int id);
    }
}