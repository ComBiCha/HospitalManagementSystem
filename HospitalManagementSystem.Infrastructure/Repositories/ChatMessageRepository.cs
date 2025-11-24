using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly HospitalDbContext _context;

        public ChatMessageRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<ChatMessage?> GetByIdAsync(int id)
        {
            return await _context.ChatMessages.FindAsync(id);
        }

        public async Task<IEnumerable<ChatMessage>> GetByIdsAsync(IEnumerable<int> ids)
        {
            return await _context.ChatMessages
                                 .Where(cm => ids.Contains(cm.Id))
                                 .ToListAsync();
        }

        public async Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(int chatRoomId) //chỉnh lại để chỉ load tin nhắn mới nhất
        {
            return await _context.ChatMessages
                                 .Include(cm => cm.Sender) // Eager load the sender information
                                 .Where(cm => cm.ChatRoomId == chatRoomId)
                                 .OrderBy(cm => cm.SentAt)
                                 .ToListAsync();
        }

        public async Task<ChatMessage> CreateAsync(ChatMessage chatMessage)
        {
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();
            return chatMessage;
        }

        public async Task<ChatMessage?> UpdateAsync(ChatMessage chatMessage)
        {
            _context.Entry(chatMessage).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.ChatMessages.AnyAsync(e => e.Id == chatMessage.Id))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
            return chatMessage;
        }

        public async Task UpdateRangeAsync(IEnumerable<ChatMessage> messages)
        {
            _context.ChatMessages.UpdateRange(messages);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var chatMessage = await _context.ChatMessages.FindAsync(id);
            if (chatMessage == null)
            {
                return false;
            }

            _context.ChatMessages.Remove(chatMessage);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}