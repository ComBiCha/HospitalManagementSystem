'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { UnpaidMedicalRecordDto } from '@/lib/types';
import { Search, ChevronLeft, ChevronRight, RefreshCw, CheckCircle, QrCode, CreditCard } from 'lucide-react';
import toast from 'react-hot-toast';
import QRCodeModal from './QRCodeModal';

interface StripePaymentState {
  showModal: boolean;
  checkoutUrl: string;
  expiresAt: string;
  paymentId: number;
}

export default function PaymentSection() {
  const [records, setRecords] = useState<UnpaidMedicalRecordDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [stripePayment, setStripePayment] = useState<StripePaymentState>({
    showModal: false,
    checkoutUrl: '',
    expiresAt: '',
    paymentId: 0
  });

  const fetchUnpaidRecords = useCallback(async (page = 1) => {
    setIsLoading(true);
    try {
      const response = await api.get(`/accountant/unpaid-medical-records?page=${page}&pageSize=10`);
      setRecords(response.data || []);
      setCurrentPage(page);
    } catch (error) {
      console.error('Error fetching unpaid medical records:', error);
      toast.error('Không thể tải danh sách thu phí.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchUnpaidRecords(1);
  }, [fetchUnpaidRecords]);

  const handleInitiateCashPayment = async (medicalRecordId: number) => {
    const toastId = toast.loading('Đang tạo thanh toán tiền mặt...');
    try {
      await api.post(`/accountant/medical-records/${medicalRecordId}/payments/cash/initiate`);
      toast.success(`Đã tạo yêu cầu thanh toán tiền mặt. Vui lòng xác nhận hoặc hủy.`);
      fetchUnpaidRecords(currentPage);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Khởi tạo thanh toán thất bại!';
      toast.error(errorMessage);
      console.error('Error initiating cash payment:', error.response?.data || error);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleInitiateStripePayment = async (medicalRecordId: number) => {
    const toastId = toast.loading('Đang tạo mã QR thanh toán Stripe...');
    try {
      const response = await api.post(`/accountant/medical-records/${medicalRecordId}/payments/stripe/initiate`);
      const { paymentId, stripeCheckoutUrl, expiresAt } = response.data;
      setStripePayment({
        showModal: true,
        checkoutUrl: stripeCheckoutUrl,
        expiresAt: expiresAt,
        paymentId: paymentId
      });
      fetchUnpaidRecords(currentPage);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Khởi tạo thanh toán Stripe thất bại!';
      toast.error(errorMessage);
      console.error('Error initiating stripe payment:', error.response?.data || error);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleConfirmCashPayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang xác nhận thanh toán...');
    try {
      await api.post(`/accountant/payments/${paymentId}/cash/confirm`, null);
      toast.success('Thanh toán thành công!');
      fetchUnpaidRecords(currentPage);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Xác nhận thanh toán thất bại!';
      toast.error(errorMessage);
      console.error('Error confirming payment:', error.response?.data || error);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleCancelCashPayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang hủy thanh toán tiền mặt...');
    try {
      await api.post(`/accountant/payments/${paymentId}/cash/cancel`, null);
      toast.success('Đã hủy thanh toán.');
      fetchUnpaidRecords(currentPage);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Hủy thanh toán thất bại!';
      toast.error(errorMessage);
      console.error('Error cancelling cash payment:', error.response?.data || error);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleCancelStripePayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang hủy thanh toán Stripe...');
    try {
      await api.post(`/accountant/payments/${paymentId}/stripe/cancel`, null);
      toast.success('Đã hủy thanh toán Stripe.');
      fetchUnpaidRecords(currentPage);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Hủy thanh toán Stripe thất bại!';
      toast.error(errorMessage);
      console.error('Error cancelling stripe payment:', error.response?.data || error);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  };

  const renderActionButtons = (record: UnpaidMedicalRecordDto) => {
    if (record.pendingPaymentId) {
      if (record.pendingPaymentMethod === 'Cash') {
        return (
          <>
            <button
              onClick={() => handleConfirmCashPayment(record.pendingPaymentId!)}
              className="text-white bg-green-600 hover:bg-green-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center"
            >
              Xác nhận
            </button>
            <button
              onClick={() => handleCancelCashPayment(record.pendingPaymentId!)}
              className="text-white bg-red-600 hover:bg-red-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center"
            >
              Hủy
            </button>
          </>
        );
      } else if (record.pendingPaymentMethod === 'Stripe') {
        return (
          <>
            <button
              onClick={() => handleInitiateStripePayment(record.medicalRecordId)}
              className="text-white bg-purple-600 hover:bg-purple-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"
            >
              <QrCode className="w-4 h-4 mr-2"/>
              Tiếp tục
            </button>
            <button
              onClick={() => handleCancelStripePayment(record.pendingPaymentId!)}
              className="text-white bg-red-600 hover:bg-red-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center"
            >
              Hủy
            </button>
          </>
        );
      }
    }

    return (
      <>
        <button
          onClick={() => handleInitiateCashPayment(record.medicalRecordId)}
          className="text-white bg-blue-600 hover:bg-blue-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"
        >
          <CreditCard className="w-4 h-4 mr-2"/>
          Tiền mặt
        </button>
        <button
          onClick={() => handleInitiateStripePayment(record.medicalRecordId)}
          className="text-white bg-purple-600 hover:bg-purple-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"
        >
          <QrCode className="w-4 h-4 mr-2"/>
          Stripe
        </button>
      </>
    );
  }

  return (
    <div className="space-y-6">
      {stripePayment.showModal && (
        <QRCodeModal
          checkoutUrl={stripePayment.checkoutUrl}
          expiresAt={stripePayment.expiresAt}
          paymentId={stripePayment.paymentId}
          onClose={() => setStripePayment({ ...stripePayment, showModal: false })}
          onPaymentSuccess={() => fetchUnpaidRecords(currentPage)}
        />
      )}
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Thu phí viện phí</h2>
        <p className="text-gray-600 mt-1">Tìm kiếm và thu phí cho các hồ sơ bệnh án chưa thanh toán.</p>
      </div>

      {/* Filter and Search Section */}
      <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-200">
        <div className="flex items-center space-x-4">
          <div className="flex-1">
            <label htmlFor="search" className="sr-only">Tìm kiếm</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <Search className="h-5 w-5 text-gray-400" />
              </div>
              <input
                type="text"
                id="search"
                className="block w-full pl-10 pr-3 py-2 border border-gray-300 rounded-md leading-5 bg-white placeholder-gray-500 focus:outline-none focus:placeholder-gray-400 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                placeholder="Tìm theo tên bệnh nhân, mã hồ sơ..."
              />
            </div>
          </div>
          <button
            onClick={() => fetchUnpaidRecords(currentPage)}
            className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
          >
            <RefreshCw className={`w-5 h-5 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
            Làm mới
          </button>
        </div>
      </div>

      {/* Unpaid Records List */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      ) : records.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl shadow-sm border">
          <CheckCircle className="mx-auto h-12 w-12 text-green-500" />
          <h3 className="mt-2 text-sm font-medium text-gray-900">Không có hồ sơ nào cần thanh toán</h3>
          <p className="mt-1 text-sm text-gray-500">Tất cả các hồ sơ bệnh án đã được thanh toán đầy đủ.</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bệnh nhân</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bác sĩ</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Ngày khám</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Phải trả</th>
                <th scope="col" className="relative px-6 py-3">
                  <span className="sr-only">Hành động</span>
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {records.map((record) => (
                <tr key={record.medicalRecordId} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-gray-900">{record.patientName}</div>
                    <div className="text-sm text-gray-500">ID: {record.patientId}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-700">{record.doctorName}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {new Date(record.appointmentDate).toLocaleDateString('vi-VN')}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-red-600">
                    {formatCurrency(record.remainingAmount)}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-2">
                    {renderActionButtons(record)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      <div className="flex items-center justify-between mt-4">
        <button
          onClick={() => fetchUnpaidRecords(currentPage - 1)}
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
          onClick={() => fetchUnpaidRecords(currentPage + 1)}
          disabled={currentPage >= totalPages || isLoading}
          className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          Sau
          <ChevronRight className="w-5 h-5 ml-2" />
        </button>
      </div>
    </div>
  );
}
