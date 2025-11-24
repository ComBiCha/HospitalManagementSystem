import { useState, FormEvent } from 'react';
import { PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { motion } from 'framer-motion';

interface StripePaymentFormProps {
  onPaymentSuccess: () => void;
  onPaymentError: (message: string) => void;
}

export default function StripePaymentForm({ onPaymentSuccess, onPaymentError }: StripePaymentFormProps) {
  const stripe = useStripe();
  const elements = useElements();

  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    if (!stripe || !elements) {
      // Stripe.js has not yet loaded.
      return;
    }

    setIsProcessing(true);

    const { error, paymentIntent } = await stripe.confirmPayment({
      elements,
      redirect: 'if_required'
    });

    if (error) {
      if (error.type === "card_error" || error.type === "validation_error") {
        setErrorMessage(error.message || 'An unknown error occurred.');
        onPaymentError(error.message || 'An unknown error occurred.');
      } else {
        setErrorMessage('An unexpected error occurred.');
        onPaymentError('An unexpected error occurred.');
      }
    } else if (paymentIntent && paymentIntent.status === 'succeeded') {
      onPaymentSuccess();
    }

    setIsProcessing(false);
  };

  return (
    <form onSubmit={handleSubmit}>
      <div className="p-6 border border-gray-200 rounded-lg bg-gray-50">
        <PaymentElement />
      </div>
      
      {errorMessage && <div className="mt-4 text-sm text-red-600 text-center">{errorMessage}</div>}

      <motion.button
        disabled={isProcessing || !stripe || !elements}
        type="submit"
        className="btn-primary w-full mt-6 text-lg font-bold disabled:opacity-50 disabled:cursor-not-allowed"
        whileTap={{ scale: 0.98 }}
      >
        {isProcessing ? "Đang xử lý..." : "Thanh toán và Xác nhận"}
      </motion.button>
    </form>
  );
}
