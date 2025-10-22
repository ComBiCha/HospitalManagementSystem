'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { MedicalRecord, PagedResult, AvailableDoctor } from '@/lib/types';
import toast from 'react-hot-toast';
import { FileText, ChevronDown, ChevronUp, DollarSign, Filter, X } from 'lucide-react';
import ImageGallery from '@/components/shared/ImageGallery';

interface MedicalRecordsSectionProps {
  patientId: number | null;
}

// Define a more specific type for our filter state
interface FilterState {
  specialty: string;
  doctorName: string;
  startDate: string;
  endDate: string;
}

export default function MedicalRecordsSection({ patientId }: MedicalRecordsSectionProps) {
  const [records, setRecords] = useState<MedicalRecord[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [expandedRecordId, setExpandedRecordId] = useState<number | null>(null);
  
  // State for filters
  const [filters, setFilters] = useState<FilterState>({ specialty: '', doctorName: '', startDate: '', endDate: '' });
  const [specialties, setSpecialties] = useState<string[]>([]);
  const [doctors, setDoctors] = useState<AvailableDoctor[]>([]);

  // State for pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [totalRecords, setTotalRecords] = useState(0);

  const fetchMedicalRecords = useCallback(async (page = 1) => {
    if (!patientId) return;
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', '10');
      if (filters.specialty) params.append('specialty', filters.specialty);
      if (filters.doctorName) params.append('doctorName', filters.doctorName);
      if (filters.startDate) params.append('startDate', filters.startDate);
      if (filters.endDate) params.append('endDate', filters.endDate);

      const response = await api.get(`/patient-portal/medical-records`, { params });
      const result: PagedResult<MedicalRecord> = response.data;

      setRecords(result.items || []);
      setCurrentPage(result.pageNumber);
      setTotalPages(result.totalPages);
      setTotalRecords(result.totalCount);

    } catch (error) {
      console.error('Error fetching medical records:', error);
      toast.error('Không thể tải hồ sơ bệnh án.');
    } finally {
      setIsLoading(false);
    }
  }, [patientId, filters]);

  useEffect(() => {
    if (patientId) {
      fetchMedicalRecords(1);
    }
  }, [patientId, fetchMedicalRecords]);

  useEffect(() => {
    // Fetch specialties on mount
    const fetchSpecialties = async () => {
      try {
        const response = await api.get('/appointmentbooking/specialties');
        setSpecialties(response.data || []);
      } catch (error) {
        console.error('Error fetching specialties:', error);
      }
    };
    fetchSpecialties();
  }, []);

  useEffect(() => {
    // Fetch doctors when specialty changes
    const fetchDoctors = async () => {
      if (filters.specialty) {
        try {
          const response = await api.get(`/appointmentbooking/doctors-by-specialty`, { params: { specialty: filters.specialty } });
          setDoctors(response.data || []);
        } catch (error) {
          console.error('Error fetching doctors:', error);
        }
      } else {
        setDoctors([]);
      }
    };
    fetchDoctors();
  }, [filters.specialty]);

  const handleFilterChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
    if (name === 'specialty') {
      // Reset doctor when specialty changes
      setFilters(prev => ({ ...prev, doctorName: '' }));
    }
  };

  const handleApplyFilters = () => {
    fetchMedicalRecords(1); // Fetch from page 1 with new filters
  };

  const handleClearFilters = () => {
    setFilters({ specialty: '', doctorName: '', startDate: '', endDate: '' });
    // The fetchMedicalRecords in the useEffect will be triggered by the change in filters
  };

  const handlePageChange = (newPage: number) => {
    if (newPage > 0 && newPage <= totalPages) {
      fetchMedicalRecords(newPage);
    }
  };

  const toggleRecord = (id: number) => {
    setExpandedRecordId(expandedRecordId === id ? null : id);
  };

  const parseJsonString = (jsonString: string, fallback: any[] = []) => {
    try {
      return JSON.parse(jsonString) || fallback;
    } catch (e) {
      return fallback;
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Hồ sơ bệnh án</h2>
        <p className="text-gray-600 mt-1">Xem lại lịch sử khám và các chi tiết điều trị của bạn.</p>
      </div>

      <FilterPanel 
        filters={filters}
        specialties={specialties}
        doctors={doctors}
        onFilterChange={handleFilterChange}
        onApply={handleApplyFilters}
        onClear={handleClearFilters}
      />

      {isLoading ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-500">Đang tải hồ sơ...</p>
        </div>
      ) : records.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <FileText className="mx-auto h-16 w-16 text-gray-400 mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ</h3>
          <p className="text-gray-500">Không tìm thấy hồ sơ bệnh án nào khớp với tiêu chí lọc của bạn.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {records.map((record) => {
            const isExpanded = expandedRecordId === record.id;
            const diagnosisList = parseJsonString(record.diagnosis);
            const prescriptionList = parseJsonString(record.prescription);

            return (
              <div key={record.id} className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <button
                  onClick={() => toggleRecord(record.id)}
                  className="w-full text-left p-6 hover:bg-gray-50 focus:outline-none"
                >
                  <div className="flex justify-between items-center">
                    <div className="flex items-center space-x-4">
                      <div className="p-3 bg-blue-100 rounded-lg">
                        <FileText className="w-6 h-6 text-blue-600" />
                      </div>
                      <div>
                        <p className="font-bold text-gray-900">Hồ sơ ngày {new Date(record.appointmentDate).toLocaleDateString('vi-VN')}</p>
                        <p className="text-sm text-gray-600">Bác sĩ: {record.doctorName}</p>
                      </div>
                    </div>
                    <div className="flex items-center space-x-4">
                      <span className={`px-3 py-1 text-xs font-semibold rounded-full ${record.appointmentStatus === 'Completed' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'}`}>
                        {record.appointmentStatus}
                      </span>
                      {isExpanded ? <ChevronUp className="w-5 h-5 text-gray-500" /> : <ChevronDown className="w-5 h-5 text-gray-500" />}
                    </div>
                  </div>
                </button>

                {isExpanded && (
                  <div className="border-t border-gray-200 p-6 space-y-6 bg-gray-50">
                    {/* Thông tin hồ sơ bệnh án */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                      <div>
                        <h4 className="text-md font-bold text-gray-800 mb-2">Chẩn đoán</h4>
                        {diagnosisList.length === 0 ? (
                          <p className="text-sm text-gray-500 italic">Chưa có chẩn đoán</p>
                        ) : (
                          <ul className="list-disc pl-5 text-sm">
                            {diagnosisList.map((d: any, idx: number) => (
                              <li key={idx}>
                                <span className="font-semibold text-emerald-700">{d.code}</span> - {d.name}
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                      <div>
                        <h4 className="text-md font-bold text-gray-800 mb-2">Đơn thuốc & Xét nghiệm</h4>
                        {prescriptionList.length === 0 ? (
                          <p className="text-sm text-gray-500 italic">Chưa có đơn thuốc hoặc xét nghiệm</p>
                        ) : (
                          <ul className="list-disc pl-5 text-sm">
                            {prescriptionList.map((p: any, idx: number) => (
                              <li key={idx}>
                                <span className={p.type === 'drug' ? 'text-pink-600 font-semibold' : 'text-blue-600 font-semibold'}>
                                  {p.name}
                                </span>
                                {p.type === 'drug' && p.quantity ? ` x${p.quantity}` : ''}
                                {p.fee ? ` (${p.fee.toLocaleString()}đ)` : ''}
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                    </div>
                    <div>
                      <h4 className="text-md font-bold text-gray-800 mb-2">Ghi chú</h4>
                      <p className="text-sm text-gray-700">{record.notes || <span className="italic text-gray-400">Không có ghi chú</span>}</p>
                    </div>
                    {/* Hình ảnh */}
                    <ImageGallery medicalRecordId={record.id} />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {totalPages > 1 && (
        <Pagination 
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={handlePageChange}
        />
      )}
    </div>
  );
}

// Filter Panel Component
interface FilterPanelProps {
  filters: FilterState;
  specialties: string[];
  doctors: AvailableDoctor[];
  onFilterChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => void;
  onApply: () => void;
  onClear: () => void;
}

function FilterPanel({ filters, specialties, doctors, onFilterChange, onApply, onClear }: FilterPanelProps) {
  return (
    <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-200 space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <select name="specialty" value={filters.specialty} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50">
          <option value="">Tất cả chuyên khoa</option>
          {specialties.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <select name="doctorName" value={filters.doctorName} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" disabled={!filters.specialty}>
          <option value="">Tất cả bác sĩ</option>
          {doctors.map(d => <option key={d.id} value={d.name}>{d.name}</option>)}
        </select>
        <input type="date" name="startDate" value={filters.startDate} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
        <input type="date" name="endDate" value={filters.endDate} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
      </div>
      <div className="flex justify-end space-x-2">
        <button onClick={onClear} className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-200 rounded-md hover:bg-gray-300 flex items-center space-x-2">
          <X className="w-4 h-4" />
          <span>Xóa bộ lọc</span>
        </button>
        <button onClick={onApply} className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 flex items-center space-x-2">
          <Filter className="w-4 h-4" />
          <span>Lọc</span>
        </button>
      </div>
    </div>
  );
}

// Pagination Component
interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function Pagination({ currentPage, totalPages, onPageChange }: PaginationProps) {
  return (
    <div className="flex justify-center items-center space-x-2 mt-6">
      <button 
        onClick={() => onPageChange(currentPage - 1)} 
        disabled={currentPage <= 1}
        className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Trước
      </button>
      <span className="text-sm text-gray-600">Trang {currentPage} / {totalPages}</span>
      <button 
        onClick={() => onPageChange(currentPage + 1)} 
        disabled={currentPage >= totalPages}
        className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Sau
      </button>
    </div>
  );
}