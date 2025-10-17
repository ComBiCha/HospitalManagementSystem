'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { Search, ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import AdvancePaymentSection from '@/components/accountant/AdvancePaymentSection';
import PendingPaymentActions from '@/components/accountant/PendingPaymentActions'; // Import the new component
import { EligibleAppointment, Paginated } from '@/lib/types';

export default function DepositSection() {
  const [appointments, setAppointments] = useState<EligibleAppointment[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchEligibleAppointments = useCallback(async (page = 1) => {
    setIsLoading(true);
    try {
      const response = await api.get(`/accountant/eligible-for-deposit-appointments?page=${page}&pageSize=5`);
      const data: Paginated<EligibleAppointment> = response.data;
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
    fetchEligibleAppointments(1);
  }, [fetchEligibleAppointments]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Thu phí tạm ứng</h2>
        <p className="text-gray-600 mt-1">Tạo các khoản thanh toán tạm ứng cho các cuộc hẹn sắp tới.</p>
      </div>

      {/* Unpaid Records List */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl shadow-sm border">
          <h3 className="mt-2 text-sm font-medium text-gray-900">Không có cuộc hẹn nào cần tạm ứng</h3>
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
                    <div className="text-sm text-gray-500">ID: {appointment.patientId}</div>
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
                      <PendingPaymentActions appointment={appointment} onSuccess={() => fetchEligibleAppointments(currentPage)} />
                    ) : (
                      <AdvancePaymentSection 
                        appointmentId={appointment.appointmentId} 
                        appointmentStatus={appointment.appointmentStatus}
                        onSuccess={() => fetchEligibleAppointments(currentPage)}
                      />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-4">
          <button
            onClick={() => fetchEligibleAppointments(currentPage - 1)}
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
            onClick={() => fetchEligibleAppointments(currentPage + 1)}
            disabled={currentPage >= totalPages || isLoading}
            className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Sau
            <ChevronRight className="w-5 h-5 ml-2" />
          </button>
        </div>
      )}
    </div>
  );
}
