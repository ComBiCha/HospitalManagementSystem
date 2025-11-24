import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { loadStripe } from '@stripe/stripe-js';
import { Elements } from '@stripe/react-stripe-js';
import StripePaymentForm from './online_booking/StripePaymentForm'; // Re-using the existing form
import { X, ServerCrash, CreditCard } from 'lucide-react';
import toast from 'react-hot-toast';
import { Appointment } from '@/lib/types';

const stripePromise = loadStripe(process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY || '');

interface PaymentModalProps {
  appointment: Appointment | null;
  onClose: () => void;
  onPaymentSuccess: () => void;
}

export default function PaymentModal({ appointment, onClose, onPaymentSuccess }: PaymentModalProps) {
  const [clientSecret, setClientSecret] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!appointment) return;

    const createPaymentIntent = async () => {
      setIsLoading(true);
      try {
        const response = await api.post('/payments/online/create-intent', {
          appointmentId: appointment.id,
        });
        setClientSecret(response.data.clientSecret);
        setError(null);
      } catch (err) {
        console.error("Error creating payment intent:", err);
        setError('Không thể khởi tạo quy trình thanh toán. Vui lòng thử lại.');
        toast.error('Đã có lỗi xảy ra. Vui lòng thử lại.');
      } finally {
        setIsLoading(false);
      }
    };

    createPaymentIntent();
  }, [appointment]);

  const appearance = {
    theme: 'stripe',
    variables: {
      colorPrimary: '#059669', // emerald-600
      colorBackground: '#ffffff',
      colorText: '#374151', // gray-700
      fontFamily: 'Inter, system-ui, sans-serif',
      borderRadius: '0.5rem', // rounded-lg
    },
  } as const;

  const options = { clientSecret, appearance };

  const handlePaymentError = (errorMessage: string) => {
    toast.error(errorMessage || 'Thanh toán không thành công.');
    onClose(); // Close the modal on payment error
  };

  const handlePaymentSuccess = () => {
    toast.success('Thanh toán thành công! Lịch hẹn đã được xác nhận.');
    onPaymentSuccess(); // This will trigger a refresh on the appointments list
    onClose();
  };

  const renderContent = () => {
    if (isLoading) {
      return (
        <div className="flex flex-col items-center justify-center h-64">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-emerald-600"></div>
          <p className="mt-4 text-gray-500">Đang chuẩn bị thanh toán...</p>
        </div>
      );
    }

    if (error) {
      return (
        <div className="flex flex-col items-center justify-center h-64 text-red-500">
          <ServerCrash className="w-12 h-12 mb-4" />
          <p className="text-center font-semibold">{error}</p>
        </div>
      );
    }

    if (clientSecret) {
      return (
        <Elements options={options} stripe={stripePromise}>
          <StripePaymentForm onPaymentSuccess={handlePaymentSuccess} onPaymentError={handlePaymentError} />
        </Elements>
      );
    }

    return null;
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl max-w-md w-full">
        <div className="sticky top-0 bg-gray-50 p-4 flex items-center justify-between rounded-t-2xl border-b">
            <h3 className="text-lg font-semibold text-gray-800 flex items-center">
                <CreditCard className="w-6 h-6 mr-3 text-emerald-600"/>
                Thanh toán phí tư vấn
            </h3>
          <button onClick={onClose} className="p-2 hover:bg-gray-200 rounded-full transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6">
            {renderContent()}
        </div>
      </div>
    </div>
  );
}
