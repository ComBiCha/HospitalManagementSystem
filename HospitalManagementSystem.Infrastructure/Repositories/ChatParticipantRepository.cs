using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class ChatParticipantRepository : IChatParticipantRepository
    {
        private readonly HospitalDbContext _context;

        public ChatParticipantRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<ChatParticipant?> GetByIdAsync(int id)
        {
            return await _context.ChatParticipants.FindAsync(id);
        }

        public async Task<IEnumerable<ChatParticipant>> GetByChatRoomIdAsync(int chatRoomId)
        {
            return await _context.ChatParticipants
                                 .Where(cp => cp.ChatRoomId == chatRoomId)
                                 .ToListAsync();
        }

        public async Task<ChatParticipant?> GetByChatRoomAndUserIdAsync(int chatRoomId, int userId)
        {
            return await _context.ChatParticipants.FirstOrDefaultAsync(cp => cp.ChatRoomId == chatRoomId && cp.UserId == userId);
        }

        public async Task<IEnumerable<ChatRoom>> GetChatRoomsByUserIdAsync(int userId)
        {
            return await _context.ChatParticipants
                                 .Where(cp => cp.UserId == userId)
                                 .Select(cp => cp.ChatRoom)
                                 .ToListAsync();
        }

        public async Task<bool> IsUserInChatRoomAsync(int chatRoomId, int userId)
        {
            return await _context.ChatParticipants.AnyAsync(cp => cp.ChatRoomId == chatRoomId && cp.UserId == userId && cp.LeftAt == null);
        }

        public async Task<ChatParticipant> CreateAsync(ChatParticipant chatParticipant)
        {
            _context.ChatParticipants.Add(chatParticipant);
            await _context.SaveChangesAsync();
            return chatParticipant;
        }

        public async Task<ChatParticipant?> UpdateAsync(ChatParticipant chatParticipant)
        {
            _context.Entry(chatParticipant).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.ChatParticipants.AnyAsync(e => e.Id == chatParticipant.Id))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
            return chatParticipant;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var chatParticipant = await _context.ChatParticipants.FindAsync(id);
            if (chatParticipant == null)
            {
                return false;
            }

            _context.ChatParticipants.Remove(chatParticipant);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}