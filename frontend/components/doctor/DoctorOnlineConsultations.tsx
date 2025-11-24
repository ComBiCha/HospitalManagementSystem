'use client';

import { useState, useEffect, useCallback } from 'react';
import { Appointment } from '@/lib/types';
import { api } from '@/lib/api';
import { Calendar, Clock, Tag, Video, MessageSquare, RefreshCw, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import ChatWindow from '@/components/chat/ChatWindow';
import { useAuth } from '@/hooks/useAuth';

interface DoctorOnlineConsultationsProps {
  doctorId: number;
}

export default function DoctorOnlineConsultations({ doctorId }: DoctorOnlineConsultationsProps) {
  const { user } = useAuth();
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [chatAppointmentId, setChatAppointmentId] = useState<number | null>(null);

  const fetchAppointments = useCallback(async (id: number) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await api.get(`/appointments/doctor/${id}`); // Assuming an API endpoint for doctor's appointments
      const onlineAppointments = response.data.filter((apt: Appointment) => apt.type === 1);
      setAppointments(onlineAppointments);
    } catch (error) {
      console.error('Error fetching doctors appointments:', error);
      setError('Error loading appointments');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (doctorId) {
      fetchAppointments(doctorId);
    }
  }, [doctorId, fetchAppointments]);

  const isChatEligible = (appointment: Appointment): boolean => {
    if (appointment.type !== 1 || appointment.status !== 'Scheduled') {
      return false;
    }
    const aptDate = new Date(appointment.date);
    const now = new Date();
    const minutesUntilAppointment = (aptDate.getTime() - now.getTime()) / (1000 * 60);
    // Chat is eligible 30 minutes before the appointment and up to 1 hour after (example duration)
    return minutesUntilAppointment <= 30 && minutesUntilAppointment >= -60;
  };

  const handleOpenChat = (appointmentId: number) => {
    setChatAppointmentId(appointmentId);
    setIsChatOpen(true);
  };

  const handleCloseChat = () => {
    setChatAppointmentId(null);
    setIsChatOpen(false);
  };

  return (
    <>
      <div className="mt-8">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-xl font-bold text-gray-800">Các cuộc tư vấn trực tuyến</h3>
          <button
            onClick={() => fetchAppointments(doctorId)}
            disabled={isLoading}
            className="flex items-center space-x-2 px-3 py-1.5 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 transition-colors font-semibold disabled:opacity-50 disabled:cursor-not-allowed text-sm"
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Làm mới</span>
          </button>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600"></div></div>
        ) : error ? (
          <div className="text-center py-8 text-red-500">Error: {error}</div>
        ) : appointments.length === 0 ? (
          <div className="text-center py-8 border-2 border-dashed rounded-xl">
            <Video className="mx-auto h-12 w-12 text-gray-300" />
            <h3 className="mt-3 text-md font-semibold text-gray-800">Chưa có cuộc tư vấn trực tuyến nào</h3>
            <p className="mt-1 text-sm text-gray-500">Các cuộc hẹn tư vấn trực tuyến của bạn sẽ xuất hiện ở đây.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {appointments
              .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
              .map((appointment) => {
                const chatEligible = isChatEligible(appointment);
                return (
                <div key={appointment.id} className="border border-gray-200 rounded-xl p-4 transition-shadow hover:shadow-md bg-white">
                  <div className="flex flex-col sm:flex-row justify-between items-start">
                    <div className="flex-1 mb-3 sm:mb-0">
                      <div className="flex items-center space-x-4 mb-3">
                        <h4 className="text-base font-bold text-gray-800">
                          {appointment.patientName || 'N/A'}
                        </h4>
                        <span className="inline-flex items-center space-x-2 px-3 py-1 text-xs font-semibold rounded-full bg-blue-100 text-blue-800">
                            <Calendar className="w-3 h-3" />
                            <span>{appointment.status}</span>
                        </span>
                      </div>
                      <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                        <div className="flex items-center space-x-2 text-gray-600">
                          <Tag className="w-4 h-4 text-emerald-600"/>
                          <span>{appointment.doctorSpecialty || 'N/A'}</span>
                        </div>
                        <div className="flex items-center space-x-2 text-gray-600">
                          <Calendar className="w-4 h-4 text-emerald-600"/>
                          <span>{new Date(appointment.date).toLocaleDateString('vi-VN')}</span>
                        </div>
                        <div className="flex items-center space-x-2 text-gray-600 col-span-2">
                          <Clock className="w-4 h-4 text-emerald-600"/>
                          <span>{new Date(appointment.date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</span>
                        </div>
                      </div>
                    </div>
                    <div className="flex flex-col items-end space-y-2 w-full sm:w-auto">
                      {chatEligible && (
                        <button 
                          onClick={() => handleOpenChat(appointment.id)}
                          className="w-full sm:w-auto flex items-center justify-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-semibold"
                        >
                          <MessageSquare className="w-4 h-4"/>
                          <span>Vào phòng chat</span>
                        </button>
                      )}
                       <p className="text-xs text-gray-400 pt-1 w-full text-right">
                        #{appointment.id}
                      </p>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {isChatOpen && chatAppointmentId && (
        <ChatWindow 
          appointmentId={chatAppointmentId}
          onClose={handleCloseChat}
        />
      )}
    </>
  );
}
