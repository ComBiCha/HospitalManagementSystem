using HospitalManagementSystem.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HospitalManagementSystem.Domain.Repositories
{
    public interface IChatMessageRepository
    {
        Task<ChatMessage?> GetByIdAsync(int id);
        Task<IEnumerable<ChatMessage>> GetByIdsAsync(IEnumerable<int> ids);
        Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(int chatRoomId);
        Task<ChatMessage> CreateAsync(ChatMessage chatMessage);
        Task<ChatMessage?> UpdateAsync(ChatMessage chatMessage);
        Task UpdateRangeAsync(IEnumerable<ChatMessage> messages);
        Task<bool> DeleteAsync(int id);
    }
}