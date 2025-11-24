import { useState } from 'react';
import { api } from '@/lib/api';
import { ArrowLeft, Calendar, Clock, User, Tag, ServerCrash } from 'lucide-react';
import { format } from 'date-fns';
import toast from 'react-hot-toast';
import { BookingData } from '@/lib/types';

interface Step4Props {
  data: BookingData;
  onBack: () => void;
  onClose: () => void;
}

export default function Step4_ConfirmAndPay({ data, onBack, onClose }: Step4Props) {
  const [isLoading, setIsLoading] = useState(false);

  const handleConfirmBooking = async () => {
    if (!data.doctor || !data.appointmentDate) {
      toast.error('Thông tin đặt khám không đầy đủ.');
      return;
    }

    setIsLoading(true);
    try {
      // Only create the appointment. The status will be 'PendingPayment' by default.
      await api.post('/appointmentbooking/online', {
        doctorId: data.doctor.id,
        date: data.appointmentDate,
      });

      toast.success('Lịch hẹn đã được tạo. Vui lòng thanh toán trong vòng 30 phút.');
      onClose(); // Close the modal
    } catch (err: any) {
      console.error("Error creating appointment:", err);
      const errorMessage = err.response?.data?.message || 'Không thể tạo lịch hẹn. Vui lòng thử lại.';
      toast.error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div className="flex items-center mb-6">
        <button onClick={onBack} className="p-2 rounded-full hover:bg-gray-100 mr-2">
          <ArrowLeft className="w-5 h-5 text-gray-600" />
        </button>
        <h3 className="text-lg font-semibold">Xác nhận thông tin lịch hẹn</h3>
      </div>

      {/* Booking Summary */}
      {data.specialty && data.doctor && data.appointmentDate && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 space-y-3 mb-6">
          <div className="flex items-center">
            <Tag className="w-5 h-5 mr-3 text-primary-600" />
            <span className="text-gray-600">Chuyên khoa:</span>
            <span className="font-bold text-gray-900 ml-auto">{data.specialty}</span>
          </div>
          <div className="flex items-center">
            <User className="w-5 h-5 mr-3 text-primary-600" />
            <span className="text-gray-600">Bác sĩ:</span>
            <span className="font-bold text-gray-900 ml-auto">{data.doctor.name}</span>
          </div>
          <div className="flex items-center">
            <Calendar className="w-5 h-5 mr-3 text-primary-600" />
            <span className="text-gray-600">Ngày:</span>
            <span className="font-bold text-gray-900 ml-auto">{format(data.appointmentDate, 'dd/MM/yyyy')}</span>
          </div>
          <div className="flex items-center">
            <Clock className="w-5 h-5 mr-3 text-primary-600" />
            <span className="text-gray-600">Thời gian:</span>
            <span className="font-bold text-gray-900 ml-auto">{format(data.appointmentDate, 'HH:mm')}</span>
          </div>
          <div className="border-t border-gray-200 my-2"></div>
          <div className="flex items-center text-xl">
            <span className="text-gray-800 font-semibold">Phí tư vấn:</span>
            <span className="font-bold text-primary-600 ml-auto">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(200000)}</span>
          </div>
           <p className="text-xs text-gray-500 text-center pt-2">
            Bạn sẽ cần thanh toán để xác nhận lịch hẹn này.
          </p>
        </div>
      )}

      <div className="mt-6 flex flex-col gap-3">
         <button
          onClick={handleConfirmBooking}
          disabled={isLoading}
          className="w-full bg-primary-600 text-white font-bold py-3 px-4 rounded-lg hover:bg-primary-700 disabled:bg-gray-400 transition-colors"
        >
          {isLoading ? 'Đang xử lý...' : 'Xác nhận và đặt hẹn'}
        </button>
        <button
          onClick={onBack}
          className="w-full bg-gray-200 text-gray-700 font-bold py-3 px-4 rounded-lg hover:bg-gray-300 transition-colors"
        >
          Quay lại
        </button>
      </div>
    </div>
  );
}