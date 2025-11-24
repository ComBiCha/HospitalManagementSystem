import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { motion } from 'framer-motion';
import { User, ServerCrash, ArrowLeft } from 'lucide-react';
import { Doctor, BookingData } from '@/lib/types';

interface Step2Props {
  data: BookingData;
  onNext: (data: Partial<BookingData>) => void;
  onBack: () => void;
}

export default function Step2_SelectDoctor({ data, onNext, onBack }: Step2Props) {
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!data.specialty) return;

    const fetchDoctors = async () => {
      try {
        setIsLoading(true);
        const response = await api.get<Doctor[]>(`/appointmentbooking/doctors-by-specialty?specialty=${data.specialty}`);
        setDoctors(response.data);
        setError(null);
      } catch (err) {
        setError('Không thể tải danh sách bác sĩ. Vui lòng thử lại sau.');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchDoctors();
  }, [data.specialty]);

  const handleSelect = (doctor: Doctor) => {
    onNext({ doctor });
  };

  if (isLoading) {
    return <div className="flex justify-center items-center h-48">
      <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600"></div>
    </div>;
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
        <h3 className="text-lg font-semibold text-center">Chọn bác sĩ cho chuyên khoa: <span className="text-primary-600">{data.specialty}</span></h3>
      </div>
      <div className="space-y-3">
        {doctors.map((doctor: Doctor, index: number) => (
          <motion.div
            key={doctor.id}
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <button 
              onClick={() => handleSelect(doctor)}
              className="w-full flex items-center p-4 border border-gray-200 rounded-lg text-left hover:shadow-md hover:border-primary-500 transition-all duration-200 bg-white"
            >
              <div className="w-12 h-12 bg-gray-100 rounded-full flex items-center justify-center mr-4">
                <User className="w-6 h-6 text-gray-500" />
              </div>
              <div>
                <p className="font-bold text-gray-800">{doctor.name}</p>
                <p className="text-sm text-gray-500">{doctor.specialty}</p>
              </div>
            </button>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
