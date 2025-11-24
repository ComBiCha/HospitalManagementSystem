import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { motion } from 'framer-motion';
import { Stethoscope, ServerCrash } from 'lucide-react';
import { BookingData } from '@/lib/types';

const specialtyIcons = {
  'default': <Stethoscope className="w-8 h-8 text-white" />
  // Add more specialty-specific icons here if needed
};

interface Step1Props {
  onNext: (data: Partial<BookingData>) => void;
}

export default function Step1_SelectSpecialty({ onNext }: Step1Props) {
  const [specialties, setSpecialties] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchSpecialties = async () => {
      try {
        setIsLoading(true);
        const response = await api.get<string[]>('/appointmentbooking/specialties');
        setSpecialties(response.data);
        setError(null);
      } catch (err) {
        setError('Không thể tải danh sách chuyên khoa. Vui lòng thử lại sau.');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };
    fetchSpecialties();
  }, []);

  const handleSelect = (specialty: string) => {
    onNext({ specialty });
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
      </div>
    );
  }

  return (
    <div>
      <h3 className="text-lg font-semibold text-center mb-6">Bạn cần tư vấn về chuyên khoa nào?</h3>
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
        {specialties.map((specialty: string, index: number) => (
          <motion.div
            key={specialty}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <button 
              onClick={() => handleSelect(specialty)}
              className="w-full p-4 border border-gray-200 rounded-lg text-center hover:shadow-lg hover:border-primary-500 hover:-translate-y-1 transition-all duration-200 group bg-white"
            >
              <div className="w-16 h-16 bg-gradient-to-br from-primary-400 to-primary-600 rounded-full mx-auto flex items-center justify-center mb-3 shadow-lg group-hover:scale-110 transition-transform duration-200">
                {specialtyIcons['default']}
              </div>
              <p className="font-semibold text-gray-700 group-hover:text-primary-600 transition-colors">{specialty}</p>
            </button>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
