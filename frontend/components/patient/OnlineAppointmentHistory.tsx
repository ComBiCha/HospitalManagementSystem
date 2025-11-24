'use client';

import { useState, useEffect, useCallback } from 'react';
import { Appointment } from '@/lib/types';
import { api } from '@/lib/api';
import PaymentModal from '@/components/patient/PaymentModal';
import { Calendar, Clock, User, Tag, AlertTriangle, CheckCircle, XCircle, RefreshCw, CreditCard, Video, Trash2, MessageSquare } from 'lucide-react'; // Added MessageSquare
import toast from 'react-hot-toast';
import ChatWindow from '@/components/chat/ChatWindow'; // Import ChatWindow

interface OnlineAppointmentHistoryProps {
  patientId: number;
  refreshKey: number;
}

export default function OnlineAppointmentHistory({ patientId, refreshKey }: OnlineAppointmentHistoryProps) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedAppointment, setSelectedAppointment] = useState<Appointment | null>(null);
  const [isPaymentModalOpen, setIsPaymentModalOpen] = useState(false);
  const [isChatOpen, setIsChatOpen] = useState(false); // State to manage chat window visibility
  const [chatAppointmentId, setChatAppointmentId] = useState<number | null>(null); // State to hold appointmentId for chat

  const fetchAppointments = useCallback(async (id: number) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await api.get(`/appointments/patient/${id}`);
      const allAppointments = response.data || [];
      const onlineAppointments = allAppointments.filter((apt: Appointment) => apt.type === 1);
      setAppointments(onlineAppointments);
    } catch (error) {
      console.error('Error fetching appointments:', error);
      setError('Error loading appointments');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (patientId) {
      fetchAppointments(patientId);
    }
  }, [patientId, fetchAppointments, refreshKey]);

  const handleOpenPaymentModal = (appointment: Appointment) => {
    setSelectedAppointment(appointment);
    setIsPaymentModalOpen(true);
  };

  const handleClosePaymentModal = () => {
    setSelectedAppointment(null);
    setIsPaymentModalOpen(false);
  };

  const handlePaymentSuccess = () => {
    handleClosePaymentModal();
    if (patientId) {
      fetchAppointments(patientId);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusMap: Record<string, { text: string; icon: React.ElementType; className: string }> = {
        PendingPayment: { text: 'Chờ thanh toán', icon: CreditCard, className: 'bg-yellow-100 text-yellow-800' },
        Scheduled: { text: 'Đã đặt lịch', icon: Calendar, className: 'bg-blue-100 text-blue-800' },
        Confirmed: { text: 'Đã xác nhận', icon: CheckCircle, className: 'bg-green-100 text-green-800' },
        Completed: { text: 'Hoàn thành', icon: CheckCircle, className: 'bg-emerald-100 text-emerald-800' },
        Cancelled: { text: 'Đã hủy', icon: XCircle, className: 'bg-red-100 text-red-800' },
        ExpiredPayment: { text: 'Hết hạn TT', icon: AlertTriangle, className: 'bg-orange-100 text-orange-800' },
        InProgress: { text: 'Đang khám', icon: RefreshCw, className: 'bg-cyan-100 text-cyan-800' },
      };
  
      const statusInfo = statusMap[status] || { text: status, icon: Calendar, className: 'bg-gray-100 text-gray-800' };
      const Icon = statusInfo.icon;
  
      return (
        <span className={`inline-flex items-center space-x-2 px-3 py-1 text-xs font-semibold rounded-full ${statusInfo.className}`}>
          <Icon className="w-3 h-3" />
          <span>{statusInfo.text}</span>
        </span>
      );
  };

  const canCancelAppointment = (appointmentDate: string): boolean => {
    const aptDate = new Date(appointmentDate);
    const now = new Date();
    const minutesUntilAppointment = (aptDate.getTime() - now.getTime()) / (1000 * 60);
    return minutesUntilAppointment >= 15; // 15 minutes cancellation policy
  };

  const isChatEligible = (appointment: Appointment): boolean => {
    if (appointment.type !== 1 || appointment.status !== 'Scheduled') {
      return false;
    }
    const aptDate = new Date(appointment.date);
    const now = new Date();
    const minutesUntilAppointment = (aptDate.getTime() - now.getTime()) / (1000 * 60);

    console.log(`Appointment ID: ${appointment.id}`);
    console.log(`  Appointment Date (parsed): ${aptDate.toLocaleString()}`);
    console.log(`  Current Date (parsed): ${now.toLocaleString()}`);
    console.log(`  Minutes Until Appointment: ${minutesUntilAppointment}`);
    console.log(`  Condition: ${minutesUntilAppointment <= 30 && minutesUntilAppointment >= -60}`);

    // Chat is eligible 30 minutes before the appointment and up to 1 hour after (example duration)
    return minutesUntilAppointment <= 30 && minutesUntilAppointment >= -60;
  };

  const handleCancelAppointment = async (appointmentId: number, appointmentDate: string) => {
    if (!canCancelAppointment(appointmentDate)) {
      toast.error('Chỉ có thể hủy lịch trước 15 phút!');
      return;
    }
    
    if (!confirm('Bạn có chắc muốn hủy lịch khám này?')) return;
    
    setIsLoading(true);
    try {
      await api.post(`/appointmentbooking/${appointmentId}/cancel`); // Call the backend POST endpoint
      toast.success('Hủy lịch khám thành công!');
      fetchAppointments(patientId);
    } catch (error: any) {
      console.error('Error cancelling appointment:', error);
      const errorMsg = error.response?.data?.message || 'Hủy lịch khám thất bại!';
      toast.error(errorMsg);
    } finally {
      setIsLoading(false);
    }
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
          <h3 className="text-xl font-bold text-gray-800">Lịch sử tư vấn trực tuyến</h3>
          <button
            onClick={() => fetchAppointments(patientId)}
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
            <h3 className="mt-3 text-md font-semibold text-gray-800">Chưa có lịch sử tư vấn</h3>
            <p className="mt-1 text-sm text-gray-500">Các lịch hẹn tư vấn trực tuyến của bạn sẽ xuất hiện ở đây.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {appointments
              .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
              .map((appointment) => {
                const canCancel = canCancelAppointment(appointment.date);
                const chatEligible = isChatEligible(appointment);
                return (
                <div key={appointment.id} className="border border-gray-200 rounded-xl p-4 transition-shadow hover:shadow-md bg-white">
                  <div className="flex flex-col sm:flex-row justify-between items-start">
                    <div className="flex-1 mb-3 sm:mb-0">
                      <div className="flex items-center space-x-4 mb-3">
                        <h4 className="text-base font-bold text-gray-800">
                          {appointment.doctorName || 'N/A'}
                        </h4>
                        {getStatusBadge(appointment.status)}
                      </div>
                      <div className="grid grid-cols2 gap-x-4 gap-y-2 text-sm">
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
                      {appointment.status === 'PendingPayment' && (
                        <button 
                          onClick={() => handleOpenPaymentModal(appointment)}
                          className="w-full sm:w-auto flex items-center justify-center space-x-2 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors font-semibold"
                        >
                          <CreditCard className="w-4 h-4"/>
                          <span>Thanh toán ngay</span>
                        </button>
                      )}
                      {appointment.status === 'Scheduled' && chatEligible && (
                        <button 
                          onClick={() => handleOpenChat(appointment.id)}
                          className="w-full sm:w-auto flex items-center justify-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-semibold"
                        >
                          <MessageSquare className="w-4 h-4"/>
                          <span>Vào phòng chat</span>
                        </button>
                      )}
                      {appointment.status === 'Scheduled' && (
                        <div className="flex items-center">
                          {canCancel ? (
                            <button
                              onClick={() => handleCancelAppointment(appointment.id, appointment.date)}
                              disabled={isLoading}
                              className="inline-flex items-center px-3 py-1.5 text-sm text-red-600 hover:text-red-700 hover:bg-red-50 rounded-md transition-colors disabled:opacity-50"
                            >
                              <Trash2 className="w-4 h-4 mr-1.5" />
                              Hủy lịch
                            </button>
                          ) : (
                            <div className="flex items-center text-xs text-gray-500">
                              <AlertTriangle className="w-4 h-4 mr-1.5" />
                              Không thể hủy (phải trước 15 phút)
                            </div>
                          )}
                        </div>
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

      {isPaymentModalOpen && (
        <PaymentModal 
          appointment={selectedAppointment}
          onClose={handleClosePaymentModal}
          onPaymentSuccess={handlePaymentSuccess}
        />
      )}

      {isChatOpen && chatAppointmentId && (
        <ChatWindow 
          appointmentId={chatAppointmentId}
          onClose={handleCloseChat}
        />
      )}
    </>
  );
}
