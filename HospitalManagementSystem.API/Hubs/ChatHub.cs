using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Domain.Entities;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.Domain.Storages; // For IStorageService
using HospitalManagementSystem.Application.DTOs;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HospitalManagementSystem.API.Hubs
{
    [Authorize] // Ensure only authenticated users can connect to the hub
    public class ChatHub : Hub
    {
        private readonly IChatRoomRepository _chatRoomRepository;
        private readonly IChatMessageRepository _chatMessageRepository;
        private readonly IChatParticipantRepository _chatParticipantRepository;
        private readonly IAuthRepository _authRepository;
        private readonly IStorageService _storageService; // For image uploads
        private readonly IAppointmentRepository _appointmentRepository; // To get appointment details
        private readonly ILogger<ChatHub> _logger;

        private static readonly ConcurrentDictionary<string, string> _connectionsInCall = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<int, string> _userConnections = new ConcurrentDictionary<int, string>();
        private static readonly ConcurrentDictionary<string, string> _pendingCalls = new ConcurrentDictionary<string, string>(); // Caller -> Callee

        public ChatHub(
            IChatRoomRepository chatRoomRepository,
            IChatMessageRepository chatMessageRepository,
            IChatParticipantRepository chatParticipantRepository,
            IAuthRepository authRepository,
            IStorageService storageService,
            IAppointmentRepository appointmentRepository,
            ILogger<ChatHub> logger)
        {
            _chatRoomRepository = chatRoomRepository;
            _chatMessageRepository = chatMessageRepository;
            _chatParticipantRepository = chatParticipantRepository;
            _authRepository = authRepository;
            _storageService = storageService;
            _appointmentRepository = appointmentRepository;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var currentUserId))
            {
                _userConnections[currentUserId] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public async Task<int> JoinChat(int appointmentId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var currentUserId))
            {
                throw new HubException("Unauthorized: User ID not found.");
            }

            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var authorizationId = currentUserId;

            if (userRole == "Patient")
            {
                var patientIdClaim = Context.User?.FindFirst("PatientId")?.Value;
                if (!string.IsNullOrEmpty(patientIdClaim) && int.TryParse(patientIdClaim, out var patientId))
                {
                    authorizationId = patientId;
                }
            }
            else if (userRole == "Doctor")
            {
                var doctorIdClaim = Context.User?.FindFirst("DoctorId")?.Value;
                if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out var doctorId))
                {
                    authorizationId = doctorId;
                }
            }

            var chatRoom = await _chatRoomRepository.GetByAppointmentIdAsync(appointmentId);
            if (chatRoom == null)
            {
                chatRoom = new ChatRoom { AppointmentId = appointmentId, CreatedAt = DateTime.UtcNow };
                await _chatRoomRepository.CreateAsync(chatRoom);
            }

            var appointment = await _appointmentRepository.GetByIdAsync(appointmentId); 
            if (appointment == null || (appointment.PatientId != authorizationId && appointment.DoctorId != authorizationId))
            {
                throw new HubException("Forbidden: You are not a participant of this appointment.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoom.Id.ToString());

            var participant = await _chatParticipantRepository.GetByChatRoomAndUserIdAsync(chatRoom.Id, currentUserId);
            if (participant == null)
            {
                participant = new ChatParticipant
                {
                    ChatRoomId = chatRoom.Id,
                    UserId = currentUserId,
                    JoinedAt = DateTime.UtcNow
                };
                await _chatParticipantRepository.CreateAsync(participant);
                await Clients.Group(chatRoom.Id.ToString()).SendAsync("UserJoined", currentUserId, Context.User?.Identity?.Name ?? "Unknown");
            }
            else
            {
                participant.LeftAt = null;
                participant.JoinedAt = DateTime.UtcNow;
                await _chatParticipantRepository.UpdateAsync(participant);
                await Clients.Group(chatRoom.Id.ToString()).SendAsync("UserJoined", currentUserId, Context.User?.Identity?.Name ?? "Unknown");
            }

            var messages = await _chatMessageRepository.GetChatHistoryAsync(chatRoom.Id);
            var messageDtos = messages.Select(ChatMessageDto.FromChatMessage);
            await Clients.Caller.SendAsync("ReceiveChatHistory", messageDtos);
            
            return chatRoom.Id;
        }

        public async Task SendMessage(int chatRoomId, string content, string? imageUrl, string messageType)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var currentUserId))
            {
                throw new HubException("Unauthorized: User ID not found.");
            }

            var isParticipant = await _chatParticipantRepository.IsUserInChatRoomAsync(chatRoomId, currentUserId);
            if (!isParticipant)
            {
                throw new HubException("Forbidden: You are not a participant of this chat.");
            }

            var chatMessage = new ChatMessage
            {
                ChatRoomId = chatRoomId,
                SenderId = currentUserId,
                Content = content,
                ImageUrl = imageUrl,
                SentAt = DateTime.UtcNow,
                MessageType = messageType,
                IsReceivedByPatient = false,
                IsSeenByPatient = false,
                IsReceivedByDoctor = false,
                IsSeenByDoctor = false
            };

            await _chatMessageRepository.CreateAsync(chatMessage);

            var sender = await _authRepository.GetUserByIdAsync(currentUserId);
            if (sender == null)
            {
                throw new HubException("Sender not found.");
            }
            chatMessage.Sender = sender;

            var messageDto = ChatMessageDto.FromChatMessage(chatMessage);

            await Clients.Group(chatRoomId.ToString()).SendAsync("ReceiveMessage", messageDto);
        }

        public async Task MarkMessageAsSeen(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var currentUserId)){
                throw new HubException("Unauthorized: User ID not found.");
            }

            var message = await _chatMessageRepository.GetByIdAsync(messageId);
            if (message == null) return;

            var chatRoom = await _chatRoomRepository.GetByIdAsync(message.ChatRoomId);
            if (chatRoom == null) return;

            var appointment = await _appointmentRepository.GetByIdAsync(chatRoom.AppointmentId);
            if (appointment == null) return;

            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var authorizationId = 0;
            if (userRole == "Patient")
            {
                var patientIdClaim = Context.User?.FindFirst("PatientId")?.Value;
                if (!string.IsNullOrEmpty(patientIdClaim) && int.TryParse(patientIdClaim, out var patientId)){
                    authorizationId = patientId;
                }
            }
            else if (userRole == "Doctor")
            {
                var doctorIdClaim = Context.User?.FindFirst("DoctorId")?.Value;
                if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out var doctorId)){
                    authorizationId = doctorId;
                }
            }

            if (authorizationId == 0) return;

            var roleOfSeer = "";
            var updated = false;

            if (userRole == "Patient" && appointment.PatientId == authorizationId && !message.IsSeenByPatient)
            {
                message.IsSeenByPatient = true;
                message.IsReceivedByPatient = true;
                roleOfSeer = "Patient";
                updated = true;
            }
            else if (userRole == "Doctor" && appointment.DoctorId == authorizationId && !message.IsSeenByDoctor)
            {
                message.IsSeenByDoctor = true;
                message.IsReceivedByDoctor = true;
                roleOfSeer = "Doctor";
                updated = true;
            }

            if (updated)
            {
                await _chatMessageRepository.UpdateAsync(message);
                await Clients.Group(chatRoom.Id.ToString()).SendAsync("MessageSeen", message.Id, roleOfSeer);
            }
        }

        public async Task MarkMessagesAsSeen(List<int> messageIds)
        {
            if (messageIds == null || !messageIds.Any()) return;

            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var currentUserId)){
                throw new HubException("Unauthorized: User ID not found.");
            }

            var messages = await _chatMessageRepository.GetByIdsAsync(messageIds);
            if (!messages.Any()) return;

            var firstMessage = messages.First();
            var chatRoom = await _chatRoomRepository.GetByIdAsync(firstMessage.ChatRoomId);
            if (chatRoom == null) return;

            var appointment = await _appointmentRepository.GetByIdAsync(chatRoom.AppointmentId);
            if (appointment == null) return;

            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var authorizationId = 0;
            if (userRole == "Patient")
            {
                var patientIdClaim = Context.User?.FindFirst("PatientId")?.Value;
                if (!string.IsNullOrEmpty(patientIdClaim) && int.TryParse(patientIdClaim, out var patientId)){
                    authorizationId = patientId;
                }
            }
            else if (userRole == "Doctor")
            {
                var doctorIdClaim = Context.User?.FindFirst("DoctorId")?.Value;
                if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out var doctorId)){
                    authorizationId = doctorId;
                }
            }

            if (authorizationId == 0 || (userRole == "Patient" && appointment.PatientId != authorizationId) || (userRole == "Doctor" && appointment.DoctorId != authorizationId)){
                 throw new HubException("Forbidden: You are not a participant of this chat.");
            }

            var updatedMessages = new List<ChatMessage>();
            foreach (var message in messages)
            {
                if (userRole == "Patient" && !message.IsSeenByPatient)
                {
                    message.IsSeenByPatient = true;
                    message.IsReceivedByPatient = true;
                    updatedMessages.Add(message);
                }
                else if (userRole == "Doctor" && !message.IsSeenByDoctor)
                {
                    message.IsSeenByDoctor = true;
                    message.IsReceivedByDoctor = true;
                    updatedMessages.Add(message);
                }
            }

            if (updatedMessages.Any())
            {
                await _chatMessageRepository.UpdateRangeAsync(updatedMessages);
                await Clients.Group(chatRoom.Id.ToString()).SendAsync("MessagesSeen", updatedMessages.Select(m => m.Id), userRole);
            }
        }

        public async Task MarkMessageAsReceived(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var currentUserId)){
                throw new HubException("Unauthorized: User ID not found.");
            }

            var message = await _chatMessageRepository.GetByIdAsync(messageId);
            if (message == null) return;

            var chatRoom = await _chatRoomRepository.GetByIdAsync(message.ChatRoomId);
            if (chatRoom == null) return;

            var appointment = await _appointmentRepository.GetByIdAsync(chatRoom.AppointmentId);
            if (appointment == null) return;

            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var authorizationId = 0;
            if (userRole == "Patient")
            {
                var patientIdClaim = Context.User?.FindFirst("PatientId")?.Value;
                if (!string.IsNullOrEmpty(patientIdClaim) && int.TryParse(patientIdClaim, out var patientId)){
                    authorizationId = patientId;
                }
            }
            else if (userRole == "Doctor")
            {
                var doctorIdClaim = Context.User?.FindFirst("DoctorId")?.Value;
                if (!string.IsNullOrEmpty(doctorIdClaim) && int.TryParse(doctorIdClaim, out var doctorId)){
                    authorizationId = doctorId;
                }
            }

            if (authorizationId == 0) return;

            var roleOfReceiver = "";
            var updated = false;

            if (userRole == "Patient" && appointment.PatientId == authorizationId && !message.IsReceivedByPatient)
            {
                message.IsReceivedByPatient = true;
                roleOfReceiver = "Patient";
                updated = true;
            }
            else if (userRole == "Doctor" && appointment.DoctorId == authorizationId && !message.IsReceivedByDoctor)
            {
                message.IsReceivedByDoctor = true;
                roleOfReceiver = "Doctor";
                updated = true;
            }

            if (updated)
            {
                await _chatMessageRepository.UpdateAsync(message);
                await Clients.Group(chatRoom.Id.ToString()).SendAsync("MessageReceived", message.Id, roleOfReceiver);
            }
        }

        #region WebRTC Signaling

        public async Task StartCall(int chatRoomId)
        {
            var callerConnectionId = Context.ConnectionId;
            var callerName = Context.User?.Identity?.Name ?? "Unknown";
            var callerId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);

            if (_connectionsInCall.ContainsKey(callerConnectionId) || _pendingCalls.ContainsKey(callerConnectionId))
            {
                await Clients.Caller.SendAsync("CallError", "You are already in a call or calling someone.");
                return;
            }

            var participants = await _chatParticipantRepository.GetByChatRoomIdAsync(chatRoomId);
            var otherParticipant = participants.FirstOrDefault(p => p.UserId != callerId);

            if (otherParticipant != null)
            {
                if (_userConnections.TryGetValue(otherParticipant.UserId, out var otherConnectionId))
                {
                    if (_connectionsInCall.ContainsKey(otherConnectionId) || _pendingCalls.Values.Contains(otherConnectionId))
                    {
                        await Clients.Caller.SendAsync("UserIsBusy");
                    }
                    else
                    {
                        _pendingCalls.TryAdd(callerConnectionId, otherConnectionId);
                        _logger.LogInformation($"Call started by {callerName} to {otherConnectionId}");
                        await Clients.Client(otherConnectionId).SendAsync("IncomingCall", callerConnectionId, callerName);
                    }
                }
                else
                {
                    _logger.LogWarning($"Could not find connection for user {otherParticipant.UserId}");
                    await Clients.Caller.SendAsync("CallError", "The other user is not online.");
                }
            }
        }

        public async Task AcceptCall(string callerConnectionId)
        {
            var calleeConnectionId = Context.ConnectionId;
            
            if (!_pendingCalls.TryRemove(callerConnectionId, out _))
            {
                // Call was already cancelled or accepted
                return;
            }

            _logger.LogInformation($"{calleeConnectionId} accepted call from {callerConnectionId}");

            _connectionsInCall.TryAdd(callerConnectionId, calleeConnectionId);
            _connectionsInCall.TryAdd(calleeConnectionId, callerConnectionId);

            await Clients.Client(callerConnectionId).SendAsync("CallAccepted", calleeConnectionId);
        }

        public async Task RejectCall(string callerConnectionId)
        {
            _logger.LogInformation($"{Context.ConnectionId} rejected call from {callerConnectionId}");
            _pendingCalls.TryRemove(callerConnectionId, out _);
            await Clients.Client(callerConnectionId).SendAsync("CallRejected");
        }

        private void CleanupCall(string connectionId)
        {
            // If the connection is in an active call, end it for both parties
            if (_connectionsInCall.TryRemove(connectionId, out var otherConnectionId))
            {
                _connectionsInCall.TryRemove(otherConnectionId, out _);
                Clients.Client(otherConnectionId).SendAsync("CallEnded");
            }
            // If the connection was a caller in a pending call, cancel it for the callee
            else if (_pendingCalls.TryRemove(connectionId, out var calleeConnectionId))
            {
                Clients.Client(calleeConnectionId).SendAsync("CallCancelled");
            }
            // If the connection was a callee in a pending call, notify the caller
            else
            {
                var pendingCall = _pendingCalls.FirstOrDefault(p => p.Value == connectionId);
                if (!string.IsNullOrEmpty(pendingCall.Key))
                {
                    _pendingCalls.TryRemove(pendingCall.Key, out _);
                    Clients.Client(pendingCall.Key).SendAsync("CallCancelled");
                }
            }
        }

        public Task EndCall()
        {
            _logger.LogInformation($"Call ended by {Context.ConnectionId}");
            CleanupCall(Context.ConnectionId);
            return Task.CompletedTask;
        }

        public async Task SendOffer(string targetConnectionId, string offer)
        {
            _logger.LogInformation($"Sending offer from {Context.ConnectionId} to {targetConnectionId}");
            await Clients.Client(targetConnectionId).SendAsync("OfferReceived", Context.ConnectionId, offer);
        }

        public async Task SendAnswer(string targetConnectionId, string answer)
        {
            _logger.LogInformation($"Sending answer from {Context.ConnectionId} to {targetConnectionId}");
            await Clients.Client(targetConnectionId).SendAsync("AnswerReceived", answer);
        }

        public async Task SendIceCandidate(string targetConnectionId, string candidate)
        {
            _logger.LogInformation($"Sending ICE candidate from {Context.ConnectionId} to {targetConnectionId}");
            await Clients.Client(targetConnectionId).SendAsync("ICECandidateReceived", candidate);
        }

        #endregion

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"Client {connectionId} disconnected.");

            CleanupCall(connectionId);

            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userId, out int currentUserId))
            {
                _userConnections.TryRemove(new KeyValuePair<int, string>(currentUserId, connectionId));

                var chatRooms = await _chatParticipantRepository.GetChatRoomsByUserIdAsync(currentUserId);
                foreach (var chatRoom in chatRooms)
                {
                    await Clients.Group(chatRoom.Id.ToString()).SendAsync("UserLeft", new { userId = currentUserId, connectionId = connectionId });

                    var participant = await _chatParticipantRepository.GetByChatRoomAndUserIdAsync(chatRoom.Id, currentUserId);
                    if (participant != null)
                    {
                        participant.LeftAt = DateTime.UtcNow;
                        await _chatParticipantRepository.UpdateAsync(participant);
                    }
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}