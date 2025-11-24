import { useState } from 'react';
import { Video } from 'lucide-react';
import OnlineBookingWizard from './online_booking/OnlineBookingWizard';
import OnlineAppointmentHistory from './OnlineAppointmentHistory';
import { Patient } from '@/lib/types';

interface OnlineBookingSectionProps {
  patient: Patient | null;
}

export default function OnlineBookingSection({ patient }: OnlineBookingSectionProps) {
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleWizardClose = () => {
    setIsWizardOpen(false);
    setRefreshKey(oldKey => oldKey + 1);
  };

  return (
    <div className="space-y-8">
      <div className="bg-white rounded-xl shadow-lg p-6 border border-gray-200">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-800">Tư vấn trực tuyến</h2>
            <p className="mt-1 text-sm text-gray-500">
              Đặt lịch hẹn với bác sĩ chuyên khoa để được tư vấn từ xa một cách tiện lợi.
            </p>
          </div>
          <Video className="w-12 h-12 text-primary-500" />
        </div>
        <div className="mt-6">
          <button 
            onClick={() => setIsWizardOpen(true)} 
            className="btn-primary w-full sm:w-auto"
          >
            Đặt lịch tư vấn ngay
          </button>
        </div>

        {isWizardOpen && (
          <OnlineBookingWizard onClose={handleWizardClose} />
        )}
      </div>

      {patient && (
        <OnlineAppointmentHistory patientId={patient.id} refreshKey={refreshKey} />
      )}
    </div>
  );
}