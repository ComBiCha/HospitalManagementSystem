'use client';

import { useState } from 'react';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { Check, X, QrCode, ExternalLink } from 'lucide-react';
import { EligibleAppointment } from '@/lib/types';
import QRCodeModal from './QRCodeModal'; // Import the modal

interface PendingPaymentActionsProps {
  appointment: EligibleAppointment;
  onSuccess: () => void; // Callback to refresh data
}

export default function PendingPaymentActions({ appointment, onSuccess }: PendingPaymentActionsProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [isQrModalOpen, setIsQrModalOpen] = useState(false);

  const handleConfirmCash = async () => {
    if (!appointment.pendingPaymentId) return;
    setIsLoading(true);
    const toastId = toast.loading('Đang xác nhận thanh toán tiền mặt...');
    try {
      await api.post(`/accountant/payments/${appointment.pendingPaymentId}/cash/confirm`);
      toast.success('Thanh toán đã được xác nhận thành công!');
      onSuccess();
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Xác nhận thanh toán thất bại.');
    } finally {
      setIsLoading(false);
      toast.dismiss(toastId);
    }
  };

  const handleCancelPayment = async () => {
    if (!appointment.pendingPaymentId) return;
    setIsLoading(true);
    const toastId = toast.loading('Đang huỷ thanh toán...');
    const endpoint = appointment.pendingPaymentMethod === 'Cash' 
      ? `/accountant/payments/${appointment.pendingPaymentId}/cash/cancel`
      : `/accountant/payments/${appointment.pendingPaymentId}/stripe/cancel`;
    try {
      await api.post(endpoint);
      toast.success('Thanh toán đã được huỷ.');
      onSuccess();
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Huỷ thanh toán thất bại.');
    } finally {
      setIsLoading(false);
      toast.dismiss(toastId);
    }
  };

  const isStripeSessionActive = appointment.stripeSessionExpiresAt && new Date(appointment.stripeSessionExpiresAt) > new Date();

  if (appointment.pendingPaymentMethod === 'Cash') {
    return (
      <div className="flex items-center space-x-2">
        <button onClick={handleConfirmCash} disabled={isLoading} className="p-2 text-green-600 hover:bg-green-100 rounded-full disabled:opacity-50">
          <Check className="h-5 w-5" />
        </button>
        <button onClick={handleCancelPayment} disabled={isLoading} className="p-2 text-red-600 hover:bg-red-100 rounded-full disabled:opacity-50">
          <X className="h-5 w-5" />
        </button>
        <span className="text-xs text-gray-500">Chờ tiền mặt</span>
      </div>
    );
  }

  if (appointment.pendingPaymentMethod === 'Stripe') {
    return (
      <>
        {isQrModalOpen && appointment.stripeCheckoutUrl && appointment.stripeSessionExpiresAt && appointment.pendingPaymentId && (
          <QRCodeModal
            checkoutUrl={appointment.stripeCheckoutUrl}
            expiresAt={appointment.stripeSessionExpiresAt}
            paymentId={appointment.pendingPaymentId}
            onClose={() => setIsQrModalOpen(false)}
            onPaymentSuccess={onSuccess}
          />
        )}
        <div className="flex items-center space-x-2">
          {isStripeSessionActive ? (
            <button onClick={() => setIsQrModalOpen(true)} disabled={isLoading} className="p-2 text-blue-600 hover:bg-blue-100 rounded-full">
              <QrCode className="h-5 w-5" />
            </button>
          ) : (
            <span className="p-2 text-gray-400">
              <QrCode className="h-5 w-5" />
            </span>
          )}
          <button onClick={handleCancelPayment} disabled={isLoading} className="p-2 text-red-600 hover:bg-red-100 rounded-full disabled:opacity-50">
            <X className="h-5 w-5" />
          </button>
          <span className={`text-xs ${isStripeSessionActive ? 'text-blue-500' : 'text-gray-500'}`}>
            {isStripeSessionActive ? 'Chờ Stripe' : 'Hết hạn'}
          </span>
        </div>
      </>
    );
  }

  // Fallback or if payment method is unknown
  return null;
}
