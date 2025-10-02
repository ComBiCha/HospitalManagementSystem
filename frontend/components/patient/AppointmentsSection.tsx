'use client';

import { useState, useEffect } from 'react';
import { api } from '@/lib/api';
import { Calendar, Clock, User, Stethoscope, X, Trash2, AlertCircle } from 'lucide-react';
import toast from 'react-hot-toast';

interface AppointmentsSectionProps {
  patientId: number | null;
}

interface Appointment {
  id: number;
  patientId: number;
  doctorId: number;
  date: string;
  status: string;
  patientName: string;
  doctorName: string;
  doctorSpecialty: string;
}

interface TimeSlot {
  time: string;
  displayTime: string;
  isAvailable: boolean;
}

interface AvailableDoctor {
  id: number;
  name: string;
  specialty: string;
  email: string;
}

export default function AppointmentsSection({ patientId }: AppointmentsSectionProps) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [showBookingModal, setShowBookingModal] = useState(false);
  
  const [step, setStep] = useState(1);
  const [selectedDate, setSelectedDate] = useState('');
  const [selectedTime, setSelectedTime] = useState('');
  const [selectedSpecialty, setSelectedSpecialty] = useState('');
  const [selectedDoctor, setSelectedDoctor] = useState<number | null>(null);
  
  const [specialties, setSpecialties] = useState<string[]>([]);
  const [timeSlots, setTimeSlots] = useState<TimeSlot[]>([]);
  const [availableDoctors, setAvailableDoctors] = useState<AvailableDoctor[]>([]);

  useEffect(() => {
    if (patientId) {
      fetchAppointments();
    }
  }, [patientId]);

  const fetchAppointments = async () => {
    if (!patientId) return;
    setIsLoading(true);
    try {
      const response = await api.get(`/appointments/patient/${patientId}`);
      setAppointments(response.data || []);
    } catch (error) {
      console.error('Error fetching appointments:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const fetchSpecialties = async () => {
    try {
      const response = await api.get('/appointmentbooking/specialties');
      setSpecialties(response.data || []);
    } catch (error) {
      console.error('Error fetching specialties:', error);
      toast.error('Không thể tải danh sách chuyên khoa');
    }
  };

  const fetchTimeSlots = async (date: string) => {
    try {
      const response = await api.get(`/appointmentbooking/available-slots?date=${date}`);
      const slots = response.data || [];
      
      const now = new Date();
      const minBookingTime = new Date(now.getTime() + 6 * 60 * 60 * 1000);
      
      const filteredSlots = slots.filter((slot: TimeSlot) => {
        const slotDateTime = new Date(`${date}T${slot.displayTime}:00`);
        return slotDateTime >= minBookingTime;
      });
      
      setTimeSlots(filteredSlots);
      
      if (filteredSlots.length === 0) {
        toast.error('Không có khung giờ nào khả dụng (phải đặt trước ít nhất 6 giờ)');
      }
    } catch (error) {
      console.error('Error fetching time slots:', error);
      toast.error('Không thể tải khung giờ');
    }
  };

  const fetchAvailableDoctors = async () => {
    if (!selectedDate || !selectedTime || !selectedSpecialty) return;
    
    try {
      const appointmentDateTime = `${selectedDate}T${selectedTime}:00Z`;
      const response = await api.get(
        `/appointmentbooking/available-doctors?appointmentDate=${appointmentDateTime}&specialty=${selectedSpecialty}`
      );
      setAvailableDoctors(response.data || []);
      
      if (response.data.length === 0) {
        toast.error('Không có bác sĩ nào available trong khung giờ này');
      }
    } catch (error) {
      console.error('Error fetching available doctors:', error);
      toast.error('Không thể tải danh sách bác sĩ');
    }
  };

  const handleOpenBookingModal = () => {
    setShowBookingModal(true);
    setStep(1);
    fetchSpecialties();
  };

  const handleNextStep = async () => {
    if (step === 1 && selectedDate) {
      await fetchTimeSlots(selectedDate);
      setStep(2);
    } else if (step === 2 && selectedTime) {
      setStep(3);
    } else if (step === 3 && selectedSpecialty) {
      await fetchAvailableDoctors();
      setStep(4);
    }
  };

  const handleBookAppointment = async () => {
    if (!patientId || !selectedDoctor || !selectedDate || !selectedTime) return;
    
    const appointmentsOnDate = appointments.filter(apt => {
      const aptDate = new Date(apt.date).toISOString().split('T')[0];
      return aptDate === selectedDate && apt.status !== 'Cancelled';
    });
    
    if (appointmentsOnDate.length >= 2) {
      toast.error('Bạn chỉ có thể đặt tối đa 2 lịch khám trong 1 ngày!');
      return;
    }
    
    const requestedTime = new Date(`${selectedDate}T${selectedTime}:00`);
    for (const apt of appointmentsOnDate) {
      const existingTime = new Date(apt.date);
      const timeDiffMinutes = Math.abs((requestedTime.getTime() - existingTime.getTime()) / (1000 * 60));
      
      if (timeDiffMinutes < 60) {
        toast.error('Các lịch khám phải cách nhau ít nhất 1 giờ!');
        return;
      }
    }
    
    setIsLoading(true);
    try {
      // Convert to local datetime first, then to ISO string
      const localDateTime = new Date(`${selectedDate}T${selectedTime}:00`);
      const appointmentDateTime = localDateTime.toISOString();
      
      // Step 1: Create appointment (Status = PendingPayment)
      const appointmentResponse = await api.post('/appointments', {
        patientId,
        doctorId: selectedDoctor,
        date: appointmentDateTime
      });
      
      const appointmentId = appointmentResponse.data.id;
      
      toast.success('Đặt lịch thành công! Đang chuyển đến trang thanh toán...');
      
      // Step 2: Create payment and get Stripe checkout URL
      const paymentResponse = await api.post('/payments/booking-fee', {
        appointmentId
      });
      
      const { checkoutUrl } = paymentResponse.data;
      
      // Step 3: Redirect to Stripe checkout
      window.location.href = checkoutUrl;
      
    } catch (error: any) {
      console.error('Error booking appointment:', error);
      const errorMsg = error.response?.data || 'Đặt lịch khám thất bại!';
      toast.error(errorMsg);
      setIsLoading(false);
    }
  };

  const resetBookingFlow = () => {
    setStep(1);
    setSelectedDate('');
    setSelectedTime('');
    setSelectedSpecialty('');
    setSelectedDoctor(null);
    setTimeSlots([]);
    setAvailableDoctors([]);
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    }).format(date);
  };

  const canCancelAppointment = (appointmentDate: string): boolean => {
    const aptDate = new Date(appointmentDate);
    const now = new Date();
    const hoursUntilAppointment = (aptDate.getTime() - now.getTime()) / (1000 * 60 * 60);
    return hoursUntilAppointment >= 6;
  };

  const handleCancelAppointment = async (appointmentId: number, appointmentDate: string) => {
    if (!canCancelAppointment(appointmentDate)) {
      toast.error('Chỉ có thể hủy lịch trước 6 giờ!');
      return;
    }
    
    if (!confirm('Bạn có chắc muốn hủy lịch khám này?')) return;
    
    setIsLoading(true);
    try {
      await api.delete(`/appointments/${appointmentId}`);
      toast.success('Hủy lịch khám thành công!');
      fetchAppointments();
    } catch (error) {
      console.error('Error cancelling appointment:', error);
      toast.error('Hủy lịch khám thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  if (!patientId) {
    return (
      <div className="space-y-6">
        <h2 className="text-2xl font-bold text-gray-900">Lịch hẹn</h2>
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <Calendar className="mx-auto h-16 w-16 text-gray-400 mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ bệnh nhân</h3>
          <p className="text-gray-500">Vui lòng tạo hồ sơ bệnh nhân để đặt lịch khám.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Lịch hẹn</h2>
          <p className="text-gray-600 mt-1">Quản lý các cuộc hẹn khám bệnh của bạn</p>
        </div>
        <button
          onClick={handleOpenBookingModal}
          className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
        >
          <Calendar className="w-5 h-5 mr-2" />
          Đặt lịch khám
        </button>
      </div>

      {/* Appointments List */}
      {isLoading && appointments.length === 0 ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      ) : appointments.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <Calendar className="mx-auto h-16 w-16 text-gray-400 mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có lịch hẹn</h3>
          <p className="text-gray-500">Bạn chưa có lịch hẹn nào được đặt.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {appointments.map((appointment) => {
            const canCancel = canCancelAppointment(appointment.date);
            return (
              <div key={appointment.id} className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center space-x-4 mb-4">
                      <div className="flex items-center text-gray-600">
                        <Calendar className="w-5 h-5 mr-2 text-blue-500" />
                        <span className="font-medium">{formatDateTime(appointment.date)}</span>
                      </div>
                      <span className={`px-3 py-1 text-xs font-semibold rounded-full ${
                        appointment.status === 'Scheduled' ? 'bg-blue-100 text-blue-800' :
                        appointment.status === 'PendingPayment' ? 'bg-yellow-100 text-yellow-800' :
                        appointment.status === 'Completed' ? 'bg-green-100 text-green-800' :
                        appointment.status === 'ExpiredPayment' ? 'bg-gray-100 text-gray-800' :
                        'bg-red-100 text-red-800'
                      }`}>
                        {appointment.status === 'PendingPayment' ? 'Chờ thanh toán' :
                         appointment.status === 'Scheduled' ? 'Đã xác nhận' :
                         appointment.status === 'ExpiredPayment' ? 'Hết hạn' :
                         appointment.status}
                      </span>
                    </div>
                    <div className="grid grid-cols-2 gap-4 mb-4">
                      <div className="flex items-center text-gray-700">
                        <User className="w-5 h-5 mr-2 text-gray-400" />
                        <div>
                          <p className="text-xs text-gray-500">Bác sĩ</p>
                          <p className="font-semibold">{appointment.doctorName}</p>
                        </div>
                      </div>
                      <div className="flex items-center text-gray-700">
                        <Stethoscope className="w-5 h-5 mr-2 text-gray-400" />
                        <div>
                          <p className="text-xs text-gray-500">Chuyên khoa</p>
                          <p className="font-semibold">{appointment.doctorSpecialty}</p>
                        </div>
                      </div>
                    </div>
                    
                    {/* Action buttons */}
                    {appointment.status === 'PendingPayment' && (
                      <div className="flex items-center space-x-3">
                        <button
                          onClick={async () => {
                            try {
                              const response = await api.post('/payments/booking-fee', {
                                appointmentId: appointment.id
                              });
                              window.location.href = response.data.checkoutUrl;
                            } catch (error) {
                              toast.error('Không thể tạo thanh toán');
                            }
                          }}
                          className="inline-flex items-center px-4 py-2 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700 transition-colors"
                        >
                          💳 Thanh toán ngay
                        </button>
                        <div className="text-xs text-orange-600">
                          ⏰ Thanh toán trước 15 phút
                        </div>
                      </div>
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
                            <AlertCircle className="w-4 h-4 mr-1.5" />
                            Không thể hủy (phải trước 6 giờ)
                          </div>
                        )}
                      </div>
                    )}
                    
                    {appointment.status === 'ExpiredPayment' && (
                      <div className="text-xs text-gray-500">
                        Hết hạn thanh toán - Vui lòng đặt lại lịch khám
                      </div>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Booking Modal */}
      {showBookingModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
            <div className="sticky top-0 bg-white border-b border-gray-200 px-6 py-4 flex justify-between items-center">
              <h3 className="text-xl font-bold text-gray-900">Đặt lịch khám</h3>
              <button onClick={() => { setShowBookingModal(false); resetBookingFlow(); }} className="text-gray-400 hover:text-gray-600">
                <X className="w-6 h-6" />
              </button>
            </div>

            <div className="p-6">
              {/* Progress Steps */}
              <div className="mb-8">
                <div className="flex items-center justify-between mb-2">
                  {['Ngày', 'Giờ', 'Chuyên khoa', 'Bác sĩ'].map((label, index) => {
                    const stepNum = index + 1;
                    return (
                      <div key={stepNum} className="flex flex-col items-center flex-1">
                        <div className={`w-10 h-10 rounded-full flex items-center justify-center font-semibold transition-all ${
                          step >= stepNum 
                            ? 'bg-blue-600 text-white shadow-md scale-110' 
                            : 'bg-gray-200 text-gray-600'
                        }`}>
                          {stepNum}
                        </div>
                        <span className={`text-xs mt-2 font-medium ${
                          step >= stepNum ? 'text-blue-600' : 'text-gray-500'
                        }`}>
                          {label}
                        </span>
                      </div>
                    );
                  })}
                </div>
                <div className="relative h-2 bg-gray-200 rounded-full mt-4">
                  <div 
                    className="absolute h-2 bg-blue-600 rounded-full transition-all duration-300"
                    style={{ width: `${((step - 1) / 3) * 100}%` }}
                  />
                </div>
              </div>

              {/* Step 1: Select Date */}
              {step === 1 && (
                <div className="space-y-4">
                  <div className="flex items-center space-x-2 mb-4">
                    <Calendar className="w-5 h-5 text-blue-600" />
                    <h4 className="font-semibold text-lg">Chọn ngày khám</h4>
                  </div>
                  <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
                    <p className="text-sm text-blue-800">
                      <AlertCircle className="w-4 h-4 inline mr-2" />
                      Lưu ý: Phải đặt lịch trước ít nhất 6 giờ. Tối đa 2 lịch/ngày, cách nhau ít nhất 1 giờ.
                    </p>
                  </div>
                  <input
                    type="date"
                    value={selectedDate}
                    onChange={(e) => setSelectedDate(e.target.value)}
                    min={new Date().toISOString().split('T')[0]}
                    className="w-full px-4 py-3 border-2 border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-lg"
                  />
                  <button
                    onClick={handleNextStep}
                    disabled={!selectedDate}
                    className="w-full px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed font-semibold transition-colors"
                  >
                    Tiếp theo →
                  </button>
                </div>
              )}

              {/* Step 2: Select Time */}
              {step === 2 && (
                <div className="space-y-4">
                  <div className="flex items-center space-x-2 mb-4">
                    <Clock className="w-5 h-5 text-blue-600" />
                    <h4 className="font-semibold text-lg">Chọn giờ khám</h4>
                  </div>
                  {timeSlots.length === 0 ? (
                    <div className="text-center py-8 text-gray-500">
                      <Clock className="w-12 h-12 mx-auto mb-3 text-gray-400" />
                      <p>Không có khung giờ nào khả dụng</p>
                    </div>
                  ) : (
                    <div className="grid grid-cols-3 gap-3 max-h-96 overflow-y-auto">
                      {timeSlots.map((slot) => (
                        <button
                          key={slot.displayTime}
                          onClick={() => setSelectedTime(slot.displayTime)}
                          className={`px-4 py-3 border-2 rounded-lg font-medium transition-all ${
                            selectedTime === slot.displayTime
                              ? 'bg-blue-600 text-white border-blue-600 shadow-md scale-105'
                              : 'border-gray-300 hover:border-blue-500 hover:bg-blue-50'
                          }`}
                        >
                          {slot.displayTime}
                        </button>
                      ))}
                    </div>
                  )}
                  <div className="flex space-x-3">
                    <button onClick={() => setStep(1)} className="flex-1 px-4 py-3 border-2 border-gray-300 rounded-lg hover:bg-gray-50 font-semibold transition-colors">
                      ← Quay lại
                    </button>
                    <button
                      onClick={handleNextStep}
                      disabled={!selectedTime}
                      className="flex-1 px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-semibold transition-colors"
                    >
                      Tiếp theo →
                    </button>
                  </div>
                </div>
              )}

              {/* Step 3: Select Specialty */}
              {step === 3 && (
                <div className="space-y-4">
                  <div className="flex items-center space-x-2 mb-4">
                    <Stethoscope className="w-5 h-5 text-blue-600" />
                    <h4 className="font-semibold text-lg">Chọn chuyên khoa</h4>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 max-h-96 overflow-y-auto">
                    {specialties.map((specialty) => (
                      <button
                        key={specialty}
                        onClick={() => setSelectedSpecialty(specialty)}
                        className={`px-4 py-4 border-2 rounded-lg text-left font-medium transition-all ${
                          selectedSpecialty === specialty
                            ? 'bg-blue-600 text-white border-blue-600 shadow-md scale-105'
                            : 'border-gray-300 hover:border-blue-500 hover:bg-blue-50'
                        }`}
                      >
                        <Stethoscope className={`w-4 h-4 inline mr-2 ${selectedSpecialty === specialty ? 'text-white' : 'text-blue-600'}`} />
                        {specialty}
                      </button>
                    ))}
                  </div>
                  <div className="flex space-x-3">
                    <button onClick={() => setStep(2)} className="flex-1 px-4 py-3 border-2 border-gray-300 rounded-lg hover:bg-gray-50 font-semibold transition-colors">
                      ← Quay lại
                    </button>
                    <button
                      onClick={handleNextStep}
                      disabled={!selectedSpecialty}
                      className="flex-1 px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-semibold transition-colors"
                    >
                      Tiếp theo →
                    </button>
                  </div>
                </div>
              )}

              {/* Step 4: Select Doctor */}
              {step === 4 && (
                <div className="space-y-4">
                  <div className="flex items-center space-x-2 mb-4">
                    <User className="w-5 h-5 text-blue-600" />
                    <h4 className="font-semibold text-lg">Chọn bác sĩ</h4>
                  </div>
                  {availableDoctors.length === 0 ? (
                    <div className="text-center py-8 text-gray-500">
                      <User className="w-12 h-12 mx-auto mb-3 text-gray-400" />
                      <p>Không có bác sĩ available trong khung giờ này</p>
                    </div>
                  ) : (
                    <div className="space-y-3 max-h-96 overflow-y-auto">
                      {availableDoctors.map((doctor) => (
                        <button
                          key={doctor.id}
                          onClick={() => setSelectedDoctor(doctor.id)}
                          className={`w-full px-5 py-4 border-2 rounded-lg text-left transition-all ${
                            selectedDoctor === doctor.id
                              ? 'bg-blue-600 text-white border-blue-600 shadow-md scale-105'
                              : 'border-gray-300 hover:border-blue-500 hover:bg-blue-50'
                          }`}
                        >
                          <div className="flex items-center">
                            <div className={`w-10 h-10 rounded-full flex items-center justify-center mr-3 ${
                              selectedDoctor === doctor.id ? 'bg-white text-blue-600' : 'bg-blue-100 text-blue-600'
                            }`}>
                              <User className="w-5 h-5" />
                            </div>
                            <div>
                              <p className="font-semibold">{doctor.name}</p>
                              <p className={`text-sm ${selectedDoctor === doctor.id ? 'text-blue-100' : 'text-gray-600'}`}>
                                {doctor.specialty}
                              </p>
                            </div>
                          </div>
                        </button>
                      ))}
                    </div>
                  )}
                  
                  {/* Summary */}
                  {selectedDoctor && (
                    <div className="bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-lg p-4 mt-4">
                      <h5 className="font-semibold text-blue-900 mb-2">Thông tin đặt lịch:</h5>
                      <div className="space-y-1 text-sm text-blue-800">
                        <p>📅 Ngày: {selectedDate}</p>
                        <p>🕐 Giờ: {selectedTime}</p>
                        <p>🏥 Chuyên khoa: {selectedSpecialty}</p>
                        <p>👨‍⚕️ Bác sĩ: {availableDoctors.find(d => d.id === selectedDoctor)?.name}</p>
                      </div>
                    </div>
                  )}
                  
                  <div className="flex space-x-3 mt-4">
                    <button onClick={() => setStep(3)} className="flex-1 px-4 py-3 border-2 border-gray-300 rounded-lg hover:bg-gray-50 font-semibold transition-colors">
                      ← Quay lại
                    </button>
                    <button
                      onClick={handleBookAppointment}
                      disabled={!selectedDoctor || isLoading}
                      className="flex-1 px-4 py-3 bg-gradient-to-r from-blue-600 to-indigo-600 text-white rounded-lg hover:from-blue-700 hover:to-indigo-700 disabled:opacity-50 font-semibold shadow-md transition-all"
                    >
                      {isLoading ? (
                        <span className="flex items-center justify-center">
                          <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
                          Đang đặt...
                        </span>
                      ) : '✓ Xác nhận đặt lịch'}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}