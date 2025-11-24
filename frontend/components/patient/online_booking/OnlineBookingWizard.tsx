import { useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { X } from 'lucide-react';
import { BookingData } from '@/lib/types';

// We will create these step components next
import Step1_SelectSpecialty from './Step1_SelectSpecialty';
import Step2_SelectDoctor from './Step2_SelectDoctor';
import Step3_SelectDateTime from './Step3_SelectDateTime';
import Step4_ConfirmAndPay from './Step4_ConfirmAndPay';

const steps = [
  { id: 1, title: 'Chọn chuyên khoa' },
  { id: 2, title: 'Chọn bác sĩ' },
  { id: 3, title: 'Chọn ngày giờ' },
  { id: 4, title: 'Xác nhận và thanh toán' },
];

interface WizardProps {
  onClose: () => void;
}

export default function OnlineBookingWizard({ onClose }: WizardProps) {
  const [currentStep, setCurrentStep] = useState(1);
  const [bookingData, setBookingData] = useState<BookingData>({ appointmentType: 1 });

  const handleNext = (data: Partial<BookingData>) => {
    setBookingData((prev: BookingData) => ({ ...prev, ...data }));
    setCurrentStep((prev) => prev + 1);
  };

  const handleBack = () => {
    setCurrentStep((prev) => prev - 1);
  };

  const renderStep = () => {
    switch (currentStep) {
      case 1:
        return <Step1_SelectSpecialty onNext={handleNext} />;
      case 2:
        return <Step2_SelectDoctor data={bookingData} onNext={handleNext} onBack={handleBack} />;
      case 3:
        return <Step3_SelectDateTime data={bookingData} onNext={handleNext} onBack={handleBack} />;
      case 4:
        return <Step4_ConfirmAndPay data={bookingData} onBack={handleBack} onClose={onClose} />;
      default:
        return null;
    }
  };

  return (
    <div className="modal-overlay">
      <motion.div
        initial={{ y: -50, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        exit={{ y: 50, opacity: 0 }}
        transition={{ type: 'spring', stiffness: 300, damping: 30 }}
        className="modal-content !max-w-2xl"
      >
        <div className="p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-2xl font-bold text-gray-800">Đặt lịch tư vấn</h2>
            <button onClick={onClose} className="p-2 rounded-full hover:bg-gray-100">
              <X className="w-6 h-6 text-gray-600" />
            </button>
          </div>

          {/* Progress Bar */}
          <div className="mb-8">
            <div className="flex items-center">
              {steps.map((step, index) => (
                <div key={step.id} className="flex items-center w-full">
                  <div className={`flex flex-col items-center ${currentStep >= step.id ? 'text-primary-600' : 'text-gray-400'}`}>
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center border-2 ${currentStep >= step.id ? 'bg-primary-600 border-primary-600 text-white' : 'border-gray-300 bg-white'}`}>
                      {step.id}
                    </div>
                    <p className="text-xs mt-2 text-center">{step.title}</p>
                  </div>
                  {index < steps.length - 1 && (
                    <div className={`flex-auto border-t-2 transition-colors duration-500 ${currentStep > index + 1 ? 'border-primary-600' : 'border-gray-300'}`}></div>
                  )}
                </div>
              ))}
            </div>
          </div>

          <AnimatePresence mode="wait">
            <motion.div
              key={currentStep}
              initial={{ x: 30, opacity: 0 }}
              animate={{ x: 0, opacity: 1 }}
              exit={{ x: -30, opacity: 0 }}
              transition={{ duration: 0.3 }}
            >
              {renderStep()}
            </motion.div>
          </AnimatePresence>
        </div>
      </motion.div>
    </div>
  );
}
