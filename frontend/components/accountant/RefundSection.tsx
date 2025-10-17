'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import { RefundableMedicalRecordDto } from '@/lib/types'; // Assuming this type is defined

export default function RefundSection() {
  const [records, setRecords] = useState<RefundableMedicalRecordDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchRefundableRecords = useCallback(async (page = 1) => {
    setIsLoading(true);
    try {
      const response = await api.get(`/accountant/refundable-medical-records?page=${page}&pageSize=10`);
      setRecords(response.data || []);
      // Assuming pagination data is in headers or needs to be calculated
      // For now, let's just handle the first page.
    } catch (error) {
      console.error('Error fetching refundable records:', error);
      toast.error('Không thể tải danh sách hoàn trả.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRefundableRecords(1);
  }, [fetchRefundableRecords]);

  const handleInitiateRefund = async (medicalRecordId: number) => {
    const toastId = toast.loading('Đang tạo yêu cầu hoàn trả...');
    try {
      await api.post(`/accountant/medical-records/${medicalRecordId}/initiate-refund`);
      toast.success('Yêu cầu hoàn trả đã được tạo.');
      fetchRefundableRecords(currentPage);
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Tạo yêu cầu hoàn trả thất bại.');
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleCompleteRefund = async (paymentId: number) => {
    const toastId = toast.loading('Đang hoàn tất hoàn trả...');
    try {
      await api.post(`/accountant/payments/${paymentId}/complete-refund`);
      toast.success('Hoàn trả đã được hoàn tất thành công!');
      fetchRefundableRecords(currentPage);
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Hoàn tất hoàn trả thất bại.');
    } finally {
      toast.dismiss(toastId);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  };

  const renderActionButtons = (record: RefundableMedicalRecordDto) => {
    if (record.refundPaymentId && record.refundPaymentStatus === 'Pending') {
      return (
        <button
          onClick={() => handleCompleteRefund(record.refundPaymentId!)}
          className="text-white bg-green-600 hover:bg-green-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center"
        >
          Hoàn tất hoàn trả
        </button>
      );
    }
    return (
      <button
        onClick={() => handleInitiateRefund(record.medicalRecordId)}
        className="text-white bg-blue-600 hover:bg-blue-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center"
      >
        Hoàn trả viện phí
      </button>
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Hoàn trả viện phí</h2>
        <p className="text-gray-600 mt-1">Quản lý và thực hiện hoàn trả cho các hồ sơ bệnh án đã thanh toán thừa.</p>
      </div>

      {isLoading ? (
        <div className="flex justify-center py-12"><div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div></div>
      ) : records.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl shadow-sm border">
          <h3 className="mt-2 text-sm font-medium text-gray-900">Không có hồ sơ nào cần hoàn trả</h3>
        </div>
      ) : (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bệnh nhân</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Tổng phí</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Đã trả</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Tiền thừa (Hoàn trả)</th>
                <th scope="col" className="relative px-6 py-3"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {records.map((record) => (
                <tr key={record.medicalRecordId} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-gray-900">{record.patientName}</div>
                    <div className="text-sm text-gray-500">ID Bệnh án: {record.medicalRecordId}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{formatCurrency(record.totalFee)}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{formatCurrency(record.paidAmount)}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-green-600">{formatCurrency(record.overpaidAmount)}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-2">
                    {renderActionButtons(record)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination can be added here if needed */}
    </div>
  );
}
