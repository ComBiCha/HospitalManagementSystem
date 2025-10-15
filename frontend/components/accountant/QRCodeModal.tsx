import { useEffect, useState } from 'react';
import QRCode from 'react-qr-code';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';

interface QRCodeModalProps {
  checkoutUrl: string;
  expiresAt: string;
  paymentId: number;
  onClose: () => void;
  onPaymentSuccess: () => void;
}

export default function QRCodeModal({ 
  checkoutUrl, 
  expiresAt, 
  paymentId, 
  onClose, 
  onPaymentSuccess 
}: QRCodeModalProps) {
  const [timeLeft, setTimeLeft] = useState('');

  useEffect(() => {
    const calculateTimeLeft = () => {
      const difference = +new Date(expiresAt) - +new Date();
      let timeLeftString = '';

      if (difference > 0) {
        const minutes = Math.floor((difference / 1000 / 60) % 60);
        const seconds = Math.floor((difference / 1000) % 60);
        timeLeftString = `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
      } else {
        timeLeftString = '00:00';
      }

      return timeLeftString;
    };

    const timer = setInterval(() => {
      setTimeLeft(calculateTimeLeft());
    }, 1000);

    // Polling for payment status
    const polling = setInterval(async () => {
      try {
        const response = await api.get(`/payments/${paymentId}/status`);
        const status = response.data;

        if (status === 'Completed') {
          toast.success('Payment completed successfully!');
          onPaymentSuccess();
          onClose();
        } else if (status === 'Failed' || status === 'Cancelled') {
          toast.error('Payment failed or was cancelled.');
          onClose();
        }

        if (+new Date(expiresAt) < +new Date()) {
            onClose();
        }

      } catch (error) {
        console.error('Error polling for payment status:', error);
      }
    }, 5000); // Poll every 5 seconds

    return () => {
      clearInterval(timer);
      clearInterval(polling);
    };
  }, [expiresAt, paymentId, onClose, onPaymentSuccess]);

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex justify-center items-center">
      <div className="bg-white p-8 rounded-lg shadow-xl text-center">
        <h2 className="text-2xl font-bold mb-4">Scan to Pay</h2>
        <div className="p-4 bg-white rounded-md">
          <QRCode value={checkoutUrl} size={256} />
        </div>
        <p className="mt-4 text-gray-600">Please scan the QR code with your mobile device to complete the payment.</p>
        <div className="mt-4">
          <p className="text-lg font-semibold text-red-600">Expires in: {timeLeft}</p>
        </div>
        <button 
          onClick={onClose} 
          className="mt-6 bg-gray-300 hover:bg-gray-400 text-gray-800 font-bold py-2 px-4 rounded"
        >
          Close
        </button>
      </div>
    </div>
  );
}
