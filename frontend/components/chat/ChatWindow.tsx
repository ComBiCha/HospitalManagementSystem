'use client';

import React, { useState, useEffect, useRef, useCallback } from 'react';
import { HubConnectionBuilder, HubConnection, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { ChatMessage, UserInfo } from '@/lib/types';
import { useAuth } from '@/hooks/useAuth'; // Assuming a useAuth hook exists
import { Send, Image as ImageIcon, Loader2, UserPlus, UserMinus, X, Check, CheckCheck, Phone, PhoneOff, Video, VideoOff, Mic, MicOff } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '@/lib/api'; // Import api
import ProxiedImage from './ProxiedImage';

interface ChatWindowProps {
  appointmentId: number;
  chatRoomId?: number; // Optional, will be created if not exists
  onClose: () => void;
}

export default function ChatWindow({ appointmentId, chatRoomId: initialChatRoomId, onClose }: ChatWindowProps) {
  const { user, isLoading: isAuthLoading } = useAuth();
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputMessage, setInputMessage] = useState('');
  const [currentChatRoomId, setCurrentChatRoomId] = useState<number | undefined>(initialChatRoomId);
  const [isLoading, setIsLoading] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const chatEndRef = useRef<HTMLDivElement>(null);

  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  // WebRTC States
  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const [localStream, setLocalStream] = useState<MediaStream | null>(null);
  const remoteStreamRef = useRef<MediaStream | null>(null);
  const remoteVideoRef = useRef<HTMLVideoElement>(null);
  const localVideoRef = useRef<HTMLVideoElement>(null);
  const [inCall, setInCall] = useState(false);
  const [incomingCall, setIncomingCall] = useState<{ callerId: string; callerName: string } | null>(null);
  const [isCallee, setIsCallee] = useState(false);
  const [callAccepted, setCallAccepted] = useState(false);
  const otherUserConnectionId = useRef<string | null>(null);
  const [isMicOn, setIsMicOn] = useState(true);
  const [isCameraOn, setIsCameraOn] = useState(true);

  useEffect(() => {
    if (localStream && localVideoRef.current) {
      localVideoRef.current.srcObject = localStream;
    }
  }, [localStream]);

  const setupPeerConnection = (stream: MediaStream) => {
    const newPeerConnection = new RTCPeerConnection({
        iceServers: [
            { urls: 'stun:stun.l.google.com:19302' },
        ],
    });

    stream.getTracks().forEach(track => newPeerConnection.addTrack(track, stream));

    newPeerConnection.onicecandidate = event => {
        if (event.candidate && otherUserConnectionId.current) {
            connection?.invoke('SendIceCandidate', otherUserConnectionId.current, JSON.stringify(event.candidate));
        }
    };

    newPeerConnection.ontrack = event => {
        if (remoteVideoRef.current) {
            remoteVideoRef.current.srcObject = event.streams[0];
        }
    };

    peerConnectionRef.current = newPeerConnection;
  };

  const startCall = async () => {
    if (inCall || incomingCall) return; 
    console.log("startCall function called");
    if (!connection || !currentChatRoomId) return;

    try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        setLocalStream(stream);
        setInCall(true);

        await setupPeerConnection(stream);

        connection.invoke('StartCall', currentChatRoomId);
    } catch (error) {
        console.error('Detailed error starting call:', error);
        if (error instanceof Error) {
            console.error(`Error name: ${error.name}`);
            console.error(`Error message: ${error.message}`);
        }
        toast.error(`Không thể bắt đầu cuộc gọi. Lỗi: ${error instanceof Error ? error.message : String(error)}`);
    }
  };

    const acceptCall = async () => {
        if (!connection || !incomingCall) return;

        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
            setLocalStream(stream);
            setInCall(true);
            setCallAccepted(true);

            await setupPeerConnection(stream);

            connection.invoke('AcceptCall', incomingCall.callerId);
            setIncomingCall(null);
        } catch (error) {
            console.error('Detailed error accepting call:', error);
            if (error instanceof Error) {
                console.error(`Error name: ${error.name}`);
                console.error(`Error message: ${error.message}`);
            }
            toast.error(`Không thể chấp nhận cuộc gọi. Lỗi: ${error instanceof Error ? error.message : String(error)}`);
        }
    };

    const rejectCall = () => {
        if (connection && incomingCall) {
            connection.invoke('RejectCall', incomingCall.callerId);
        }
        setIncomingCall(null);
    };

  const hangUp = () => {
    // If the user is not in a call but has an incoming call, treat this as a rejection.
    if (!inCall && incomingCall) {
        rejectCall();
        return;
    }

    connection?.invoke('EndCall').catch(err => console.error('Error sending EndCall signal:', err));

    if (peerConnectionRef.current) {
        peerConnectionRef.current.close();
        peerConnectionRef.current = null;
    }

    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        setLocalStream(null);
    }

    if (localVideoRef.current) {
        localVideoRef.current.srcObject = null;
    }
    if (remoteVideoRef.current) {
        remoteVideoRef.current.srcObject = null;
    }

    setInCall(false);
    setCallAccepted(false);
    setIncomingCall(null);
    setIsCallee(false);
    otherUserConnectionId.current = null;
    toast.success('Cuộc gọi đã kết thúc.');
  };

  const toggleCamera = () => {
    if (localStream) {
        localStream.getVideoTracks().forEach(track => {
            track.enabled = !isCameraOn;
        });
        setIsCameraOn(!isCameraOn);
    }
  };

  const toggleMic = () => {
    if (localStream) {
        localStream.getAudioTracks().forEach(track => {
            track.enabled = !isMicOn;
        });
        setIsMicOn(!isMicOn);
    }
  };

  useEffect(() => {
    if (incomingCall) {
        const toastId = toast.custom((t) => (
            <div className="bg-white p-4 rounded-lg shadow-lg flex items-center">
                <div className="flex-1">
                    <p className="font-semibold">Cuộc gọi đến từ {incomingCall.callerName}</p>
                </div>
                <div className="flex space-x-2">
                    <button 
                        onClick={() => {
                            acceptCall();
                            toast.dismiss(t.id);
                        }}
                        className="p-2 bg-green-500 text-white rounded-full"
                    >
                        <Phone className="w-5 h-5" />
                    </button>
                    <button 
                        onClick={() => {
                            rejectCall();
                            toast.dismiss(t.id);
                        }}
                        className="p-2 bg-red-500 text-white rounded-full"
                    >
                        <PhoneOff className="w-5 h-5" />
                    </button>
                </div>
            </div>
        ), { duration: Infinity });

        return () => {
            toast.dismiss(toastId);
        };
    }
  }, [incomingCall]);

  // 1. Create the connection object
  useEffect(() => {
    if (isAuthLoading) {
      return;
    }
    if (!user) {
      toast.error('Bạn cần đăng nhập để tham gia chat.');
      onClose();
      return;
    }

    const getSignalRUrl = () => {
      const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://127.0.0.1:50646'; 
      const cleanBaseUrl = baseUrl.replace(/\/api$/, '');
      return `${cleanBaseUrl}/chatHub`;
    };

    const newConnection = new HubConnectionBuilder()
      .withUrl(getSignalRUrl(), {
        accessTokenFactory: () => localStorage.getItem('token') || '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    setConnection(newConnection);

  }, [user, isAuthLoading, onClose]);

  // Effect for cleaning up blob URL
  useEffect(() => {
    return () => {
      if (imagePreview) {
        URL.revokeObjectURL(imagePreview);
      }
    };
  }, [imagePreview]);

  // 2. Start and stop the connection
  useEffect(() => {
    if (connection && connection.state === HubConnectionState.Disconnected) {
      connection.start()
        .then(() => {
          console.log('SignalR Connected!');
          setIsLoading(false);
        })
        .catch(err => {
          console.error('SignalR Connection Error: ', err);
          toast.error('Không thể kết nối đến phòng chat.');
          setIsLoading(false);
          onClose();
        });
    }

    return () => {
      // The connection is stopped when the component unmounts.
      if (connection) {
         connection.stop()
          .then(() => console.log('SignalR Disconnected on unmount.'))
          .catch(err => console.error('Error stopping SignalR connection on unmount: ', err));
      }
    };
  }, [connection, onClose]);

  // 3. Register event listeners
  useEffect(() => {
    if (!connection || !user) return;

    const receiveHistoryHandler = (history: ChatMessage[]) => {
      setMessages(history);
      const unreadMessageIds = history
        .filter(msg => 
          msg.senderId !== user.id && 
          (user.role === 'Patient' ? !msg.isSeenByPatient : !msg.isSeenByDoctor)
        )
        .map(msg => msg.id);

      if (unreadMessageIds.length > 0) {
        connection.invoke('MarkMessagesAsSeen', unreadMessageIds)
          .catch(err => console.error('Error marking messages as seen:', err));
      }
    };

    const receiveMessageHandler = (message: ChatMessage) => {
      setMessages(prevMessages => [...prevMessages, message]);
      if (message.senderId !== user.id) {
        connection.invoke('MarkMessageAsReceived', message.id).catch(err => console.error('Error marking message as received:', err));
        connection.invoke('MarkMessageAsSeen', message.id).catch(err => console.error('Error marking message as seen:', err));
      }
    };

    const userJoinedHandler = (userId: number, username: string) => {
      setMessages(prevMessages => [...prevMessages, {
        id: Date.now(),
        chatRoomId: currentChatRoomId || appointmentId,
        senderId: 0, // System sender
        content: `${username} đã tham gia cuộc trò chuyện.`,
        sentAt: new Date().toISOString(),
        messageType: 'system',
        isReceivedByPatient: true, isSeenByPatient: true, isReceivedByDoctor: true, isSeenByDoctor: true,
        sender: { id: 0, firstName: 'System', lastName: '', role: 'System' } // Dummy sender
      } as ChatMessage]);
      toast(`${username} đã tham gia chat.`, { icon: '👋' });
    };

    const userLeftHandler = (userId: number, username: string) => {
      setMessages(prevMessages => [...prevMessages, {
        id: Date.now(),
        chatRoomId: currentChatRoomId || appointmentId,
        senderId: 0, // System sender
        content: `${username} đã rời cuộc trò chuyện.`,
        sentAt: new Date().toISOString(),
        messageType: 'system',
        isReceivedByPatient: true, isSeenByPatient: true, isReceivedByDoctor: true, isSeenByDoctor: true,
        sender: { id: 0, firstName: 'System', lastName: '', role: 'System' } // Dummy sender
      } as ChatMessage]);
      toast(`${username} đã rời chat.`, { icon: '🚪' });
    };

    const messageReceivedHandler = (messageId: number, role: string) => {
      setMessages(prev => prev.map(msg => 
        msg.id === messageId 
          ? { ...msg, isReceivedByDoctor: role === 'Doctor' || msg.isReceivedByDoctor, isReceivedByPatient: role === 'Patient' || msg.isReceivedByPatient }
          : msg
      ));
    };

    const messageSeenHandler = (messageId: number, role: string) => {
      setMessages(prev => prev.map(msg => 
        msg.id === messageId 
          ? { ...msg, isSeenByDoctor: role === 'Doctor' || msg.isSeenByDoctor, isSeenByPatient: role === 'Patient' || msg.isSeenByPatient }
          : msg
      ));
    };

    const messagesSeenHandler = (messageIds: number[], role: string) => {
      setMessages(prev => prev.map(msg => {
        if (messageIds.includes(msg.id)) {
          return { ...msg, isSeenByDoctor: role === 'Doctor' || msg.isSeenByDoctor, isSeenByPatient: role === 'Patient' || msg.isSeenByPatient };
        }
        return msg;
      }));
    };

    connection.on('ReceiveChatHistory', receiveHistoryHandler);
    connection.on('ReceiveMessage', receiveMessageHandler);
    connection.on('UserJoined', userJoinedHandler);
    connection.on('UserLeft', userLeftHandler);
    connection.on('MessageReceived', messageReceivedHandler);
    connection.on('MessageSeen', messageSeenHandler);
    connection.on('MessagesSeen', messagesSeenHandler);

    // Clean up listeners when the effect re-runs or component unmounts
    return () => {
      connection.off('ReceiveChatHistory');
      connection.off('ReceiveMessage');
      connection.off('UserJoined');
      connection.off('UserLeft');
      connection.off('MessageReceived');
      connection.off('MessageSeen');
      connection.off('MessagesSeen');
    };
  }, [connection, user, appointmentId, currentChatRoomId]);

    // WebRTC useEffect
    useEffect(() => {
        if (!connection || !user) return;

        const handleIncomingCall = (callerId: string, callerName: string) => {
            if (inCall) {
                connection.invoke('RejectCall', callerId);
                return;
            }
            console.log(`Incoming call from ${callerName} (${callerId})`);
            setIncomingCall({ callerId, callerName });
            setIsCallee(true);
            otherUserConnectionId.current = callerId;
        };

        const handleCallAccepted = async (calleeConnectionId: string) => {
            console.log('Call accepted by', calleeConnectionId);
            otherUserConnectionId.current = calleeConnectionId;
            if (peerConnectionRef.current) {
                const offer = await peerConnectionRef.current.createOffer();
                await peerConnectionRef.current.setLocalDescription(offer);
                connection.invoke('SendOffer', calleeConnectionId, JSON.stringify(offer));
            }
        };

        const handleCallRejected = () => {
            toast.error('Cuộc gọi đã bị từ chối.');
            hangUp();
        };

        const handleOfferReceived = async (callerId: string, offerJson: string) => {
            console.log('Offer received from', callerId);
            otherUserConnectionId.current = callerId;
            if (peerConnectionRef.current) {
                const offer = JSON.parse(offerJson);
                await peerConnectionRef.current.setRemoteDescription(new RTCSessionDescription(offer));
                const answer = await peerConnectionRef.current.createAnswer();
                await peerConnectionRef.current.setLocalDescription(answer);
                connection.invoke('SendAnswer', callerId, JSON.stringify(answer));
            }
        };

        const handleAnswerReceived = async (answerJson: string) => {
            console.log('Answer received');
            if (peerConnectionRef.current) {
                const answer = JSON.parse(answerJson);
                await peerConnectionRef.current.setRemoteDescription(new RTCSessionDescription(answer));
            }
        };

        const handleIceCandidateReceived = (candidateJson: string) => {
            console.log('ICE candidate received');
            if (peerConnectionRef.current) {
                const candidate = JSON.parse(candidateJson);
                peerConnectionRef.current.addIceCandidate(new RTCIceCandidate(candidate));
            }
        };

        const handleCallEnded = () => {
            toast.error("Cuộc gọi đã được kết thúc bởi người dùng khác.");
            hangUp(); 
        };

        const handleCallCancelled = () => {
            toast.error("Cuộc gọi không được trả lời.");
            hangUp();
        };

        const handleUserIsBusy = () => {
            toast.error('Người dùng này đang bận.');
        };

        const handleCallError = (message: string) => {
            toast.error(message);
        };

        connection.on('IncomingCall', handleIncomingCall);
        connection.on('CallAccepted', handleCallAccepted);
        connection.on('CallRejected', handleCallRejected);
        connection.on('OfferReceived', handleOfferReceived);
        connection.on('AnswerReceived', handleAnswerReceived);
        connection.on('ICECandidateReceived', handleIceCandidateReceived);
        connection.on('CallEnded', handleCallEnded);
        connection.on('CallCancelled', handleCallCancelled);
        connection.on('UserIsBusy', handleUserIsBusy);
        connection.on('CallError', handleCallError);

        return () => {
            connection.off('IncomingCall');
            connection.off('CallAccepted');
            connection.off('CallRejected');
            connection.off('OfferReceived');
            connection.off('AnswerReceived');
            connection.off('ICECandidateReceived');
            connection.off('CallEnded');
            connection.off('CallCancelled');
            connection.off('UserIsBusy');
            connection.off('CallError');
        };
    }, [connection, user, inCall]);

  // 4. Join the specific chat room
  useEffect(() => {
    if (connection && connection.state === HubConnectionState.Connected && appointmentId) {
        connection.invoke<number>('JoinChat', appointmentId)
          .then((newChatRoomId) => {
            if (newChatRoomId) {
              console.log('Joined new chat room with ID:', newChatRoomId);
              setCurrentChatRoomId(newChatRoomId);
            }
          })
          .catch(err => {
            console.error('Error joining chat room:', err);
            toast.error(`Không thể tham gia phòng chat: ${err.message}`);
          });
    }
  }, [connection, connection?.state, appointmentId]);

  const scrollToBottom = useCallback(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  const handleImageSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (imagePreview) {
      URL.revokeObjectURL(imagePreview);
    }

    setSelectedFile(file);
    const previewUrl = URL.createObjectURL(file);
    setImagePreview(previewUrl);
    e.target.value = ''; // Allow selecting the same file again
  };

  const cancelImage = () => {
    if (imagePreview) {
      URL.revokeObjectURL(imagePreview);
    }
    setImagePreview(null);
    setSelectedFile(null);
  };

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    const hasText = inputMessage.trim() !== '';
    const hasImage = selectedFile !== null;

    if ((!hasText && !hasImage) || !connection || !currentChatRoomId) return;

    setIsSending(true);
    let finalImageUrl: string | null = null;

    try {
      if (hasImage && selectedFile) {
        const formData = new FormData();
        formData.append('file', selectedFile);
        const response = await api.post('chatfiles/upload-image', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });
        finalImageUrl = response.data.imageUrl; // Expects "chatfiles/image/{fid}"
      }

      const messageType = hasText ? (hasImage ? 'text_image' : 'text') : 'image';
      await connection.invoke('SendMessage', currentChatRoomId, inputMessage.trim(), finalImageUrl, messageType);

      setInputMessage('');
      cancelImage();

    } catch (err) {
      console.error('Error sending message:', err);
      toast.error('Gửi tin nhắn thất bại.');
    } finally {
      setIsSending(false);
    }
  };

  const renderStatus = (msg: ChatMessage) => {
    if (!user || msg.senderId !== user.id) return null;

    const isOtherUserDoctor = user.role === 'Patient';
    
    if (isOtherUserDoctor) {
      if (msg.isSeenByDoctor) return <CheckCheck className="w-4 h-4 text-blue-400" />;
      if (msg.isReceivedByDoctor) return <CheckCheck className="w-4 h-4" />;
    } else { // Current user is Doctor, other is Patient
      if (msg.isSeenByPatient) return <CheckCheck className="w-4 h-4 text-blue-400" />;
      if (msg.isReceivedByPatient) return <CheckCheck className="w-4 h-4" />;
    }
    
    return <Check className="w-4 h-4" />;
  };

  if (isLoading) {
    return (
      <div className="modal-overlay">
        <div className="modal-content !max-w-2xl flex items-center justify-center p-8">
          <Loader2 className="w-8 h-8 animate-spin text-primary-500" />
          <p className="ml-2 text-gray-600">Đang kết nối phòng chat...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-overlay">
      <div className="modal-content !max-w-2xl flex flex-col h-[80vh]">
        <div className="flex items-center justify-between p-4 border-b border-gray-200">
          <h2 className="text-xl font-semibold text-gray-800">Chat với Bác sĩ/Bệnh nhân</h2>
          <div className="flex items-center space-x-2">
            {!inCall && !incomingCall ? (
                <button onClick={startCall} className="p-2 rounded-full hover:bg-gray-100">
                    <Video className="w-6 h-6 text-green-600" />
                </button>
            ) : (
                <button onClick={hangUp} className="p-2 rounded-full hover:bg-gray-100">
                    <VideoOff className="w-6 h-6 text-red-600" />
                </button>
            )}
            <button onClick={onClose} className="p-2 rounded-full hover:bg-gray-100">
                <X className="w-6 h-6 text-gray-600" />
            </button>
        </div>
        </div>

        {inCall && (
            <div className="relative p-4 bg-black">
                <video ref={remoteVideoRef} autoPlay playsInline className="w-full h-auto rounded-lg" />
                <video ref={localVideoRef} autoPlay playsInline muted className="absolute bottom-4 right-4 w-1/4 h-auto border-2 border-white rounded-lg" />
                
                {/* Call Controls */}
                <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex items-center space-x-4 bg-gray-800/50 p-2 rounded-full">
                    <button onClick={toggleMic} className="p-2 rounded-full bg-white/20 hover:bg-white/30">
                        {isMicOn ? <Mic className="w-6 h-6 text-white" /> : <MicOff className="w-6 h-6 text-red-500" />}
                    </button>
                    <button onClick={toggleCamera} className="p-2 rounded-full bg-white/20 hover:bg-white/30">
                        {isCameraOn ? <Video className="w-6 h-6 text-white" /> : <VideoOff className="w-6 h-6 text-red-500" />}
                    </button>
                    <button onClick={hangUp} className="p-2 rounded-full bg-red-600 hover:bg-red-700">
                        <PhoneOff className="w-6 h-6 text-white" />
                    </button>
                </div>
            </div>
        )}

        <div className="flex-1 p-4 overflow-y-auto bg-gray-50">
          <div className="space-y-4">
            {messages.map((msg, index) => (
              <div key={msg.id || index} className={`flex ${msg.senderId === user?.id ? 'justify-end' : 'justify-start'}`}>
                {msg.messageType === 'system' ? (
                  <div className="text-center text-sm text-gray-500 w-full">
                    <span className="bg-gray-200 px-3 py-1 rounded-full">{msg.content}</span>
                  </div>
                ) : (
                  <div className={`flex flex-col max-w-[70%] p-3 rounded-lg shadow-sm ${msg.senderId === user?.id ? 'bg-primary-500 text-white' : 'bg-white text-gray-800'}`}>
                    <div className="text-xs opacity-80 mb-1">
                      {msg.senderId === user?.id ? 'Bạn' : `${msg.sender.role === 'Doctor' ? 'Bác sĩ' : 'Bệnh nhân'} ${msg.sender.lastName} ${msg.sender.firstName}`}
                    </div>
                    {msg.content && <p className="text-sm">{msg.content}</p>}
                    {msg.imageUrl && (
                      <div className="mt-2">
                        <ProxiedImage src={msg.imageUrl} alt="Chat Image" width={200} height={150} className="rounded-lg object-cover" />
                      </div>
                    )}
                    <div className={`text-xs mt-1 ${msg.senderId === user?.id ? 'text-primary-100' : 'text-gray-500'} flex justify-end items-center space-x-2`}>
                      <span>{new Date(msg.sentAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</span>
                      {renderStatus(msg)}
                    </div>
                  </div>
                )}
              </div>
            ))}
            <div ref={chatEndRef} />
          </div>
        </div>

        <form onSubmit={handleSend} className="p-4 border-t border-gray-200 bg-white">
          {imagePreview && (
            <div className="relative mb-2 w-fit">
              <img src={imagePreview} alt="Preview" className="max-h-24 rounded-lg" />
              <button
                type="button"
                onClick={cancelImage}
                className="absolute -top-2 -right-2 bg-gray-600/70 text-white rounded-full p-0.5 hover:bg-gray-800 transition-all"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          )}
          <div className="flex items-center">
            <input
              type="text"
              value={inputMessage}
              onChange={(e) => setInputMessage(e.target.value)}
              placeholder="Nhập tin nhắn..."
              className="flex-1 border border-gray-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
              disabled={!connection || connection.state !== HubConnectionState.Connected || isSending}
            />
            <label htmlFor="image-upload" className="ml-2 p-2 text-gray-600 hover:bg-gray-100 rounded-full cursor-pointer">
              <ImageIcon className="w-6 h-6" />
              <input
                id="image-upload"
                type="file"
                accept="image/*"
                className="hidden"
                onChange={handleImageSelect}
                disabled={!connection || connection.state !== HubConnectionState.Connected || isSending}
              />
            </label>
            <button
              type="submit"
              className="ml-2 p-2 bg-primary-500 text-white rounded-full hover:bg-primary-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              disabled={!connection || connection.state !== HubConnectionState.Connected || isSending || (inputMessage.trim() === '' && !selectedFile)}
            >
              {isSending ? <Loader2 className="w-6 h-6 animate-spin" /> : <Send className="w-6 h-6" />}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}