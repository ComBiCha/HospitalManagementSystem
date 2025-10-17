'use client';

import { useState } from 'react';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { CreditCard, QrCode, Wallet, X } from 'lucide-react';

interface AdvancePaymentSectionProps {
  appointmentId: number;
  appointmentStatus: string;
  onSuccess?: () => void; // Optional callback to refresh data on parent
}

export default function AdvancePaymentSection({ appointmentId, appointmentStatus, onSuccess }: AdvancePaymentSectionProps) {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [suggestedAmount, setSuggestedAmount] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleOpenModal = async () => {
    setIsLoading(true);
    try {
      const response = await api.get(`/accountant/appointments/${appointmentId}/advance-payment-suggestion`);
      setSuggestedAmount(response.data.suggestedAmount);
      setIsModalOpen(true);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Không thể lấy số tiền tạm ứng gợi ý.';
      toast.error(errorMessage);
      console.error('Error fetching advance payment suggestion:', error.response?.data || error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleInitiatePayment = async (method: 'cash' | 'stripe') => {
    const toastId = toast.loading(`Đang tạo tạm ứng bằng ${method === 'cash' ? 'tiền mặt' : 'Stripe'}...`);
    setIsLoading(true);
    try {
      const response = await api.post(`/accountant/appointments/${appointmentId}/advance-payments/${method}/initiate`);
      if (method === 'cash') {
        toast.success('Đã tạo yêu cầu tạm ứng bằng tiền mặt. Vui lòng xác nhận khi bệnh nhân thanh toán.');
      } else {
        toast.success('Đã tạo link thanh toán Stripe! Vui lòng mở link để hoàn tất.');
        window.open(response.data.stripeCheckoutUrl, '_blank');
      }
      setIsModalOpen(false);
      onSuccess?.(); // Call the callback to refresh parent data
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Tạo tạm ứng thất bại.';
      toast.error(errorMessage);
      console.error(`Error initiating ${method} advance payment:`, error.response?.data || error);
    } finally {
      setIsLoading(false);
      toast.dismiss(toastId);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  };

  const isEligible = !['Completed', 'Cancelled', 'ExpiredPayment'].includes(appointmentStatus);

  if (!isEligible) {
    return (
        <button className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-gray-400 bg-gray-200 cursor-not-allowed" disabled>
            <Wallet className="mr-2 h-4 w-4" />
            Tạo tạm ứng
        </button>
    );
  }

  return (
    <>
      <button onClick={handleOpenModal} disabled={isLoading} className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500">
        <Wallet className="mr-2 h-4 w-4" />
        {isLoading ? 'Đang tải...' : 'Tạo tạm ứng'}
      </button>

      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-40 flex justify-center items-center">
          <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md m-4 z-50">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-lg font-bold text-gray-900">Xác nhận tạo khoản tạm ứng</h3>
              <button onClick={() => setIsModalOpen(false)} className="text-gray-400 hover:text-gray-600">
                <X className="h-6 w-6" />
              </button>
            </div>
            <p className="text-sm text-gray-600 mb-4">
              Một khoản tạm ứng sẽ được tạo cho cuộc hẹn này. Bệnh nhân có thể thanh toán ngay hoặc sau đó.
            </p>
            
            <div className="my-6 text-center">
              <p className="text-gray-600">Số tiền tạm ứng gợi ý:</p>
              <p className="text-3xl font-bold text-blue-600">
                {suggestedAmount !== null ? formatCurrency(suggestedAmount) : 'Đang tải...'}
              </p>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-6">
              <button 
                onClick={() => handleInitiatePayment('cash')} 
                disabled={isLoading || suggestedAmount === null}
                className="inline-flex items-center justify-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50"
              >
                <CreditCard className="mr-2 h-4 w-4" />
                {isLoading ? 'Đang xử lý...' : 'Tạm ứng tiền mặt'}
              </button>
              <button 
                onClick={() => handleInitiatePayment('stripe')} 
                disabled={isLoading || suggestedAmount === null}
                className="inline-flex items-center justify-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
              >
                <QrCode className="mr-2 h-4 w-4" />
                {isLoading ? 'Đang xử lý...' : 'Tạm ứng qua Stripe'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
