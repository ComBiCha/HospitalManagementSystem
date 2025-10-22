'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { UnpaidMedicalRecordDto, PaginatedResultDto } from '@/lib/types';
import { Search, ChevronLeft, ChevronRight, RefreshCw, CheckCircle, QrCode, CreditCard, Filter, X } from 'lucide-react';
import toast from 'react-hot-toast';
import QRCodeModal from './QRCodeModal';

interface StripePaymentState {
  showModal: boolean;
  checkoutUrl: string;
  expiresAt: string;
  paymentId: number;
}

interface FilterState {
  patientName: string;
  medicalRecordId: string;
  paymentStatus: string;
  startDate: string;
  endDate: string;
}

export default function PaymentSection() {
  const [records, setRecords] = useState<UnpaidMedicalRecordDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filters, setFilters] = useState<FilterState>({ patientName: '', medicalRecordId: '', paymentStatus: '', startDate: '', endDate: '' });
  const [stripePayment, setStripePayment] = useState<StripePaymentState>({ showModal: false, checkoutUrl: '', expiresAt: '', paymentId: 0 });

  const fetchUnpaidRecords = useCallback(async (page = 1, appliedFilters: FilterState) => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', '10');
      if (appliedFilters.patientName) params.append('patientName', appliedFilters.patientName);
      if (appliedFilters.medicalRecordId) params.append('medicalRecordId', appliedFilters.medicalRecordId);
      if (appliedFilters.paymentStatus) params.append('paymentStatus', appliedFilters.paymentStatus);
      if (appliedFilters.startDate) params.append('startDate', appliedFilters.startDate);
      if (appliedFilters.endDate) params.append('endDate', appliedFilters.endDate);

      const response = await api.get(`/accountant/unpaid-medical-records`, { params });
      const data: PaginatedResultDto<UnpaidMedicalRecordDto> = response.data;
      setRecords(data.items || []);
      setCurrentPage(data.page);
      setTotalPages(Math.ceil(data.totalCount / data.pageSize));
    } catch (error) {
      console.error('Error fetching unpaid medical records:', error);
      toast.error('Không thể tải danh sách thu phí.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchUnpaidRecords(1, filters);
  }, [fetchUnpaidRecords]);

  const handleFilterChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
  };

  const handleApplyFilters = () => {
    setCurrentPage(1);
    fetchUnpaidRecords(1, filters);
  };

  const handleClearFilters = () => {
    const clearedFilters = { patientName: '', medicalRecordId: '', paymentStatus: '', startDate: '', endDate: '' };
    setFilters(clearedFilters);
    setCurrentPage(1);
    fetchUnpaidRecords(1, clearedFilters);
  };

  const handlePageChange = (newPage: number) => {
    setCurrentPage(newPage);
    fetchUnpaidRecords(newPage, filters);
  }

  // ... (rest of the handler functions: handleInitiateCashPayment, handleInitiateStripePayment, etc. remain the same, but with fetchUnpaidRecords(currentPage, filters) )
  const handleInitiateCashPayment = async (medicalRecordId: number) => {
    const toastId = toast.loading('Đang tạo thanh toán tiền mặt...');
    try {
      await api.post(`/accountant/medical-records/${medicalRecordId}/payments/cash/initiate`);
      toast.success(`Đã tạo yêu cầu thanh toán tiền mặt. Vui lòng xác nhận hoặc hủy.`);
      fetchUnpaidRecords(currentPage, filters);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Khởi tạo thanh toán thất bại!';
      toast.error(errorMessage);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleInitiateStripePayment = async (medicalRecordId: number) => {
    const toastId = toast.loading('Đang tạo mã QR thanh toán Stripe...');
    try {
      const response = await api.post(`/accountant/medical-records/${medicalRecordId}/payments/stripe/initiate`);
      const { paymentId, stripeCheckoutUrl, expiresAt } = response.data;
      setStripePayment({ showModal: true, checkoutUrl: stripeCheckoutUrl, expiresAt: expiresAt, paymentId: paymentId });
      fetchUnpaidRecords(currentPage, filters);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Khởi tạo thanh toán Stripe thất bại!';
      toast.error(errorMessage);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleConfirmCashPayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang xác nhận thanh toán...');
    try {
      await api.post(`/accountant/payments/${paymentId}/cash/confirm`, null);
      toast.success('Thanh toán thành công!');
      fetchUnpaidRecords(currentPage, filters);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Xác nhận thanh toán thất bại!';
      toast.error(errorMessage);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleCancelCashPayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang hủy thanh toán tiền mặt...');
    try {
      await api.post(`/accountant/payments/${paymentId}/cash/cancel`, null);
      toast.success('Đã hủy thanh toán.');
      fetchUnpaidRecords(currentPage, filters);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Hủy thanh toán thất bại!';
      toast.error(errorMessage);
    } finally {
      toast.dismiss(toastId);
    }
  };

  const handleCancelStripePayment = async (paymentId: number) => {
    const toastId = toast.loading('Đang hủy thanh toán Stripe...');
    try {
      await api.post(`/accountant/payments/${paymentId}/stripe/cancel`, null);
      toast.success('Đã hủy thanh toán Stripe.');
      fetchUnpaidRecords(currentPage, filters);
    } catch (error: any) {
      const errorMessage = error.response?.data?.message || 'Hủy thanh toán Stripe thất bại!';
      toast.error(errorMessage);
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
            <button onClick={() => handleConfirmCashPayment(record.pendingPaymentId!)} className="text-white bg-green-600 hover:bg-green-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center">Xác nhận</button>
            <button onClick={() => handleCancelCashPayment(record.pendingPaymentId!)} className="text-white bg-red-600 hover:bg-red-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center">Hủy</button>
          </>
        );
      } else if (record.pendingPaymentMethod === 'Stripe') {
        return (
          <>
            <button onClick={() => handleInitiateStripePayment(record.medicalRecordId)} className="text-white bg-purple-600 hover:bg-purple-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"><QrCode className="w-4 h-4 mr-2"/>Tiếp tục</button>
            <button onClick={() => handleCancelStripePayment(record.pendingPaymentId!)} className="text-white bg-red-600 hover:bg-red-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center">Hủy</button>
          </>
        );
      }
    }
    return (
      <>
        <button onClick={() => handleInitiateCashPayment(record.medicalRecordId)} className="text-white bg-blue-600 hover:bg-blue-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"><CreditCard className="w-4 h-4 mr-2"/>Tiền mặt</button>
        <button onClick={() => handleInitiateStripePayment(record.medicalRecordId)} className="text-white bg-purple-600 hover:bg-purple-700 font-medium rounded-lg text-sm px-5 py-2.5 text-center inline-flex items-center"><QrCode className="w-4 h-4 mr-2"/>Stripe</button>
      </>
    );
  }

  return (
    <div className="space-y-6">
      {stripePayment.showModal && <QRCodeModal checkoutUrl={stripePayment.checkoutUrl} expiresAt={stripePayment.expiresAt} paymentId={stripePayment.paymentId} onClose={() => setStripePayment({ ...stripePayment, showModal: false })} onPaymentSuccess={() => fetchUnpaidRecords(currentPage, filters)} />}
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Thu phí viện phí</h2>
        <p className="text-gray-600 mt-1">Tìm kiếm và thu phí cho các hồ sơ bệnh án chưa thanh toán.</p>
      </div>

      <FilterPanel filters={filters} onFilterChange={handleFilterChange} onApply={handleApplyFilters} onClear={handleClearFilters} />

      {isLoading ? (
        <div className="flex justify-center py-12"><div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div></div>
      ) : records.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl shadow-sm border"><CheckCircle className="mx-auto h-12 w-12 text-green-500" /><h3 className="mt-2 text-sm font-medium text-gray-900">Không có hồ sơ nào khớp bộ lọc</h3><p className="mt-1 text-sm text-gray-500">Tất cả các hồ sơ bệnh án đã được thanh toán đầy đủ hoặc không tìm thấy.</p></div>
      ) : (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bệnh nhân</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bác sĩ</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Ngày khám</th>
                <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Phải trả</th>
                <th scope="col" className="relative px-6 py-3"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {records.map((record) => (
                <tr key={record.medicalRecordId} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap"><div className="text-sm font-medium text-gray-900">{record.patientName}</div><div className="text-sm text-gray-500">ID BN: {record.patientId} | ID HS: {record.medicalRecordId}</div></td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-700">{record.doctorName}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{new Date(record.appointmentDate).toLocaleDateString('vi-VN')}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-red-600">{formatCurrency(record.remainingAmount)}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-2">{renderActionButtons(record)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={handlePageChange} isLoading={isLoading} />}
    </div>
  );
}

interface FilterPanelProps {
  filters: FilterState;
  onFilterChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => void;
  onApply: () => void;
  onClear: () => void;
}

function FilterPanel({ filters, onFilterChange, onApply, onClear }: FilterPanelProps) {
  return (
    <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-200 space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <input type="text" name="patientName" placeholder="Tên bệnh nhân" value={filters.patientName} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
        <input type="number" name="medicalRecordId" placeholder="ID Hồ sơ" value={filters.medicalRecordId} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
        <select name="paymentStatus" value={filters.paymentStatus} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50">
          <option value="">Tất cả trạng thái TT</option>
          <option value="Unpaid">Chưa thanh toán</option>
          <option value="PartiallyPaid">Thanh toán một phần</option>
        </select>
        <input type="date" name="startDate" value={filters.startDate} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
        <input type="date" name="endDate" value={filters.endDate} onChange={onFilterChange} className="w-full p-2 border rounded-md bg-gray-50" />
      </div>
      <div className="flex justify-end space-x-2">
        <button onClick={onClear} className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-200 rounded-md hover:bg-gray-300 flex items-center space-x-2"><X className="w-4 h-4" /><span>Xóa bộ lọc</span></button>
        <button onClick={onApply} className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 flex items-center space-x-2"><Filter className="w-4 h-4" /><span>Lọc</span></button>
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
      <button onClick={() => onPageChange(currentPage - 1)} disabled={currentPage <= 1 || isLoading} className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"><ChevronLeft className="w-5 h-5 mr-2" />Trước</button>
      <span className="text-sm text-gray-700">Trang <span className="font-medium">{currentPage}</span> / <span className="font-medium">{totalPages}</span></span>
      <button onClick={() => onPageChange(currentPage + 1)} disabled={currentPage >= totalPages || isLoading} className="inline-flex items-center px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">Sau<ChevronRight className="w-5 h-5 ml-2" /></button>
    </div>
  )
}