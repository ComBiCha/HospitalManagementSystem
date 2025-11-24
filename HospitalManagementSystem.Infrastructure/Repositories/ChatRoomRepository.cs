using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace HospitalManagementSystem.Infrastructure.Repositories
{
    public class ChatRoomRepository : IChatRoomRepository
    {
        private readonly HospitalDbContext _context;

        public ChatRoomRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public async Task<ChatRoom?> GetByIdAsync(int id)
        {
            return await _context.ChatRooms.FindAsync(id);
        }

        public async Task<ChatRoom?> GetByAppointmentIdAsync(int appointmentId)
        {
            return await _context.ChatRooms.FirstOrDefaultAsync(cr => cr.AppointmentId == appointmentId);
        }

        public async Task<ChatRoom> CreateAsync(ChatRoom chatRoom)
        {
            _context.ChatRooms.Add(chatRoom);
            await _context.SaveChangesAsync();
            return chatRoom;
        }

        public async Task<ChatRoom?> UpdateAsync(ChatRoom chatRoom)
        {
            _context.Entry(chatRoom).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.ChatRooms.AnyAsync(e => e.Id == chatRoom.Id))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
            return chatRoom;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var chatRoom = await _context.ChatRooms.FindAsync(id);
            if (chatRoom == null)
            {
                return false;
            }

            _context.ChatRooms.Remove(chatRoom);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}