'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { Search, ChevronLeft, ChevronRight, RefreshCw, Filter, X } from 'lucide-react';
import toast from 'react-hot-toast';
import AdvancePaymentSection from '@/components/accountant/AdvancePaymentSection';
import PendingPaymentActions from '@/components/accountant/PendingPaymentActions';
import { EligibleAppointment, PaginatedResultDto } from '@/lib/types';

interface FilterState {
  patientName: string;
  appointmentId: string;
  startDate: string;
  endDate: string;
}

export default function DepositSection() {
  const [appointments, setAppointments] = useState<EligibleAppointment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filters, setFilters] = useState<FilterState>({ patientName: '', appointmentId: '', startDate: '', endDate: '' });

  const fetchEligibleAppointments = useCallback(async (page = 1, appliedFilters: FilterState) => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', '5');
      if (appliedFilters.patientName) params.append('patientName', appliedFilters.patientName);
      if (appliedFilters.appointmentId) params.append('appointmentId', appliedFilters.appointmentId);
      if (appliedFilters.startDate) params.append('startDate', appliedFilters.startDate);
      if (appliedFilters.endDate) params.append('endDate', appliedFilters.endDate);

      const response = await api.get(`/accountant/eligible-for-deposit-appointments`, { params });
      const data: PaginatedResultDto<EligibleAppointment> = response.data;
      
      setAppointments(data.items || []);
      setCurrentPage(data.page);
      setTotalPages(Math.ceil(data.totalCount / data.pageSize));
    } catch (error) {
      console.error('Error fetching eligible appointments:', error);
      toast.error('Không thể tải danh sách cuộc hẹn tạm ứng.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchEligibleAppointments(1, filters);
  }, [fetchEligibleAppointments]);

  const handleFilterChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
  };

  const handleApplyFilters = () => {
    setCurrentPage(1);
    fetchEligibleAppointments(1, filters);
  };

  const handleClearFilters = () => {
    const clearedFilters = { patientName: '', appointmentId: '', startDate: '', endDate: '' };
    setFilters(clearedFilters);
    setCurrentPage(1);
    fetchEligibleAppointments(1, clearedFilters);
  };

  const handlePageChange = (newPage: number) => {
    setCurrentPage(newPage);
    fetchEligibleAppointments(newPage, filters);
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Thu phí tạm ứng</h2>
        <p className="text-gray-600 mt-1">Tạo các khoản thanh toán tạm ứng cho các cuộc hẹn sắp tới.</p>
      </div>

      <FilterPanel 
        filters={filters}
        onFilterChange={handleFilterChange}
        onApply={handleApplyFilters}
        onClear={handleClearFilters}
      />

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl shadow-sm border">
          <h3 className="mt-2 text-sm font-medium text-gray-900">Không có cuộc hẹn nào khớp với bộ lọc</h3>
        </div>
      ) : (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bệnh nhân</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bác sĩ</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Ngày hẹn</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Trạng thái</th>
                <th scope="col" className="relative px-6 py-3">
                  <span className="sr-only">Hành động</span>
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {appointments.map((appointment) => (
                <tr key={appointment.appointmentId} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-gray-900">{appointment.patientName}</div>
                    <div className="text-sm text-gray-500">ID BN: {appointment.patientId}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-700">{appointment.doctorName}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {new Date(appointment.appointmentDate).toLocaleString('vi-VN')}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                     <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${appointment.appointmentStatus === 'Scheduled' ? 'bg-blue-100 text-blue-800' : 'bg-yellow-100 text-yellow-800'}`}>
                      {appointment.appointmentStatus}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                    {appointment.pendingPaymentId ? (
                      <PendingPaymentActions appointment={appointment} onSuccess={() => fetchEligibleAppointments(currentPage, filters)} />
                    ) : (
                      <AdvancePaymentSection 
                        appointmentId={appointment.appointmentId} 
                        appointmentStatus={appointment.appointmentStatus}
                        onSuccess={() => fetchEligibleAppointments(currentPage, filters)}
                      />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={handlePageChange} isLoading={isLoading} />
      )}
    </div>
  );
}


interface FilterPanelProps {
  filters: FilterState;
  onFilterChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onApply: () => void;
  onClear: () => void;
}

function FilterPanel({ filters, onFilterChange, onApply, onClear }: FilterPanelProps) {
  return (
    <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-200 space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <input type="text" name="patientName" placeholder="Tên bệnh nhân" value={filters.patientName} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
        <input type="number" name="appointmentId" placeholder="ID Cuộc hẹn" value={filters.appointmentId} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
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

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  isLoading: boolean;
}

function Pagination({ currentPage, totalPages, onPageChange, isLoading }: PaginationProps) {
  return (
    <div className="flex items-center justify-between mt-4">
      <button
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1 || isLoading}
        className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <ChevronLeft className="w-5 h-5 mr-2" />
        Trước
      </button>
      <span className="text-sm text-gray-700">
        Trang <span className="font-medium">{currentPage}</span> / <span className="font-medium">{totalPages}</span>
      </span>
      <button
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage >= totalPages || isLoading}
        className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Sau
        <ChevronRight className="w-5 h-5 ml-2" />
      </button>
    </div>
  )
}