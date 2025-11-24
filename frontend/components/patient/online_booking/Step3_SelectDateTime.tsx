import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { motion } from 'framer-motion';
import { Calendar, Clock, ServerCrash, ArrowLeft } from 'lucide-react';
import { format } from 'date-fns';
import { vi } from 'date-fns/locale';
import { BookingData } from '@/lib/types';

interface TimeSlot {
  time: string;
  displayTime: string;
}

interface Step3Props {
  data: BookingData;
  onNext: (data: Partial<BookingData>) => void;
  onBack: () => void;
}

export default function Step3_SelectDateTime({ data, onNext, onBack }: Step3Props) {
  const [availableDates, setAvailableDates] = useState<Date[]>([]);
  const [selectedDate, setSelectedDate] = useState<Date | null>(null);
  const [availableSlots, setAvailableSlots] = useState<TimeSlot[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<TimeSlot | null>(null);
  const [isLoadingDates, setIsLoadingDates] = useState(true);
  const [isLoadingSlots, setIsLoadingSlots] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const { doctor } = data;
    if (!doctor) return;

    const fetchDates = async () => {
      try {
        setIsLoadingDates(true);
        setError(null);
        const response = await api.get<string[]>(`/appointmentbooking/doctors/${doctor.id}/available-dates`);
        setAvailableDates(response.data.map(d => new Date(d)));
      } catch (err) {
        setError('Không thể tải ngày khám của bác sĩ.');
        console.error(err);
      } finally {
        setIsLoadingDates(false);
      }
    };

    fetchDates();
  }, [data.doctor]);

  useEffect(() => {
    const { doctor, appointmentType } = data;
    if (!selectedDate || !doctor || appointmentType === undefined) {
      setAvailableSlots([]);
      return;
    };

    const fetchSlots = async () => {
      try {
        setIsLoadingSlots(true);
        setError(null);
        const dateString = format(selectedDate, 'yyyy-MM-dd');
        const response = await api.get<TimeSlot[]>(`/appointmentbooking/doctors/${doctor.id}/available-slots?date=${dateString}&appointmentType=${appointmentType}`);
        setAvailableSlots(response.data);
      } catch (err) {
        setError('Không thể tải khung giờ khám.');
        console.error(err);
      } finally {
        setIsLoadingSlots(false);
      }
    };

    fetchSlots();
  }, [selectedDate, data.doctor, data.appointmentType]);

  const handleSelectDate = (date: Date) => {
    setSelectedDate(date);
    setSelectedSlot(null);
  };

  const handleSelectSlot = (slot: TimeSlot) => {
    setSelectedSlot(slot);
    if (selectedDate) {
      const [hours, minutes] = slot.displayTime.split(':');
      const finalDate = new Date(selectedDate);
      finalDate.setHours(parseInt(hours, 10), parseInt(minutes, 10), 0, 0);
      onNext({ appointmentDate: finalDate });
    }
  };

  const renderDateContent = () => {
    if (isLoadingDates) {
      return <div className="flex justify-center items-center h-24"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div></div>;
    }
    if (!availableDates.length) {
      return <p className="text-center text-gray-500">Bác sĩ hiện không có lịch khám trong thời gian tới.</p>
    }
    return (
      <div className="flex flex-wrap gap-2">
        {availableDates.map((date, index) => (
          <motion.button
            key={index}
            onClick={() => handleSelectDate(date)}
            className={`px-3 py-2 rounded-lg border-2 transition-colors ${selectedDate?.getTime() === date.getTime() ? 'bg-primary-600 border-primary-600 text-white' : 'bg-white border-gray-200 hover:border-primary-400'}`}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <span className="font-semibold">{format(date, 'dd/MM')}</span>
            <span className="text-sm ml-2">{format(date, 'EEEE', { locale: vi })}</span>
          </motion.button>
        ))}
      </div>
    );
  }

  const renderSlotsContent = () => {
    if (isLoadingSlots) {
      return <div className="flex justify-center items-center h-32"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div></div>;
    }
    if (!availableSlots.length) {
      return <p className="text-center text-gray-500 mt-4">Không có khung giờ trống cho ngày này.</p>
    }
    return (
      <div className="grid grid-cols-3 sm:grid-cols-4 gap-3">
        {availableSlots.map((slot, index) => (
          <motion.button
            key={index}
            onClick={() => handleSelectSlot(slot)}
            className={`p-3 rounded-lg border-2 text-center font-mono text-lg transition-colors ${selectedSlot?.time === slot.time ? 'bg-primary-600 border-primary-600 text-white' : 'bg-white border-gray-200 hover:border-primary-400'}`}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            {slot.displayTime}
          </motion.button>
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center h-48 text-red-500">
        <ServerCrash className="w-12 h-12 mb-4" />
        <p className="text-center">{error}</p>
        <button onClick={onBack} className="btn-secondary mt-4">Quay lại</button>
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center mb-6">
        <button onClick={onBack} className="p-2 rounded-full hover:bg-gray-100 mr-2">
          <ArrowLeft className="w-5 h-5 text-gray-600" />
        </button>
        <h3 className="text-lg font-semibold text-center">Chọn ngày và giờ khám cho bác sĩ: <span className="text-primary-600">{data.doctor?.name}</span></h3>
      </div>

      <div className="space-y-6">
        <div>
          <div className="flex items-center text-gray-700 mb-3">
            <Calendar className="w-5 h-5 mr-2" />
            <h4 className="font-semibold">Chọn ngày</h4>
          </div>
          {renderDateContent()}
        </div>

        {selectedDate && (
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
            <div className="flex items-center text-gray-700 mb-3">
              <Clock className="w-5 h-5 mr-2" />
              <h4 className="font-semibold">Chọn khung giờ</h4>
            </div>
            {renderSlotsContent()}
          </motion.div>
        )}
      </div>
    </div>
  );
}
