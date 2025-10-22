'use client';

import { useEffect, useState, useCallback } from 'react';
import toast from 'react-hot-toast';
import { api } from '@/lib/api';
import { Appointment } from '@/lib/types';
import {
  Calendar,
  Clock,
  Filter,
  Search,
  User,
  X,
  ChevronDown,
  AlertCircle,
  CheckCircle,
  XCircle,
  RefreshCw,
  Eye,
  Stethoscope,
} from 'lucide-react';

const initialFilters = {
  startDate: '',
  endDate: '',
  status: 'all',
  searchTerm: '',
};

export default function AppointmentsSection() {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [filters, setFilters] = useState(initialFilters);
  const [selectedAppointment, setSelectedAppointment] = useState<Appointment | null>(null);
  const [showPatientModal, setShowPatientModal] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchAppointments = useCallback(async (page = 1, appliedFilters = filters) => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });

      if (appliedFilters.startDate) {
        const start = new Date(appliedFilters.startDate);
        start.setHours(0, 0, 0, 0);
        params.append('startDate', start.toISOString());
      }
      if (appliedFilters.endDate) {
        const end = new Date(appliedFilters.endDate);
        end.setHours(23, 59, 59, 999);
        params.append('endDate', end.toISOString());
      }
      if (appliedFilters.status && appliedFilters.status !== 'all') {
        params.append('status', appliedFilters.status);
      }
      if (appliedFilters.searchTerm) {
        params.append('patientName', appliedFilters.searchTerm);
      }

      const response = await api.get('/Appointments/my-appointments', { params });

      setAppointments(response.data.appointments);
      setTotalCount(response.data.pagination.totalCount);
      setTotalPages(response.data.pagination.totalPages);
      setCurrentPage(response.data.pagination.currentPage);
    } catch (error: any) {
      console.error('Error fetching appointments:', error);
      toast.error('Không thể tải lịch khám!');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAppointments(currentPage, filters);
  }, [currentPage, fetchAppointments]);

  const handleFilterChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
  };

  const handleApplyFilters = () => {
    if (currentPage !== 1) {
        setCurrentPage(1);
    } else {
        fetchAppointments(1, filters);
    }
  };

  const handleClearFilters = () => {
    setFilters(initialFilters);
    if (currentPage !== 1) {
        setCurrentPage(1);
    } else {
        fetchAppointments(1, initialFilters);
    }
  };

  const getStatusBadge = (status: string) => {
    const badges: Record<string, { bg: string; text: string; icon: any }> = {
      Scheduled: { bg: 'bg-blue-100', text: 'text-blue-700', icon: Clock },
      Confirmed: { bg: 'bg-green-100', text: 'text-green-700', icon: CheckCircle },
      Completed: { bg: 'bg-emerald-100', text: 'text-emerald-700', icon: CheckCircle },
      Cancelled: { bg: 'bg-red-100', text: 'text-red-700', icon: XCircle },
      Hospitalized: { bg: 'bg-purple-100', text: 'text-purple-700', icon: AlertCircle },
      ExpiredPayment: { bg: 'bg-orange-100', text: 'text-orange-700', icon: XCircle },
      InProgress: { bg: 'bg-cyan-100', text: 'text-cyan-700', icon: RefreshCw },
    };

    const badge = badges[status] || badges.Scheduled;
    const Icon = badge.icon;

    return (
      <span className={`inline-flex items-center space-x-1 px-3 py-1 rounded-full text-xs font-semibold ${badge.bg} ${badge.text}`}>
        <Icon className="w-3 h-3" />
        <span>{getStatusText(status)}</span>
      </span>
    );
  };

  const getStatusText = (status: string) => {
    const texts: Record<string, string> = {
      Scheduled: 'Đã đặt',
      Confirmed: 'Đã xác nhận',
      Completed: 'Hoàn thành',
      Cancelled: 'Đã hủy',
      Hospitalized: 'Nhập viện',
      ExpiredPayment: 'Hết hạn TT',
      InProgress: 'Đang khám',
    };
    return texts[status] || status;
  };

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  };

  const handleViewPatient = async (appointment: Appointment) => {
    setSelectedAppointment(appointment);
    setShowPatientModal(true);
  };

  const handleStartExamination = async (appointmentId: number, status: string) => {
    try {
      if (status === 'Completed') {
        const recordResponse = await api.get(`/MedicalRecords/by-appointment/${appointmentId}`);
        window.location.href = `/doctor/examination/${recordResponse.data.id}`;
        return;
      }
      
      const checkResponse = await api.get(`/MedicalRecords/can-start-examination/${appointmentId}`);
      if (!checkResponse.data.canStart) {
        toast.error(checkResponse.data.message);
        return;
      }

      if (checkResponse.data.medicalRecordId) {
        window.location.href = `/doctor/examination/${checkResponse.data.medicalRecordId}`;
        return;
      }

      const response = await api.post('/MedicalRecords/start-examination', { appointmentId });
      toast.success(response.data.message);
      window.location.href = `/doctor/examination/${response.data.medicalRecord.id}`;
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Không thể bắt đầu khám!');
    }
  };

  if (isLoading && appointments.length === 0) {
    return (
      <div className="bg-white rounded-2xl shadow-xl p-8 border border-emerald-100">
        <div className="flex items-center justify-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-emerald-600"></div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header & Filters */}
      <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Từ ngày</label>
            <input
              type="date"
              name="startDate"
              value={filters.startDate}
              onChange={handleFilterChange}
              className="w-full px-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Đến ngày</label>
            <input
              type="date"
              name="endDate"
              value={filters.endDate}
              onChange={handleFilterChange}
              className="w-full px-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Trạng thái</label>
            <select
              name="status"
              value={filters.status}
              onChange={handleFilterChange}
              className="w-full px-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="all">Tất cả</option>
              <option value="Scheduled">Đã đặt</option>
              <option value="Confirmed">Đã xác nhận</option>
              <option value="InProgress">Đang khám</option>
              <option value="Completed">Hoàn thành</option>
              <option value="Hospitalized">Nhập viện</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">Tìm kiếm</label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
              <input
                type="text"
                name="searchTerm"
                value={filters.searchTerm}
                onChange={handleFilterChange}
                placeholder="Tên bệnh nhân..."
                className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between">
          <p className="text-sm text-gray-600">
            Tìm thấy <span className="font-bold text-emerald-600">{totalCount}</span> lịch khám
          </p>
          <div className="flex items-center space-x-2">
            <button onClick={handleClearFilters} className="flex items-center space-x-2 px-4 py-2 bg-gray-200 text-gray-700 rounded-xl hover:bg-gray-300 transition-colors font-semibold">
                <X className="w-4 h-4" />
                <span>Xóa lọc</span>
            </button>
            <button onClick={handleApplyFilters} className="flex items-center space-x-2 px-4 py-2 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors font-semibold">
                <Filter className="w-4 h-4" />
                <span>Lọc</span>
            </button>
          </div>
        </div>
      </div>

      {/* Appointments List */}
      <div className="space-y-4">
        {isLoading && <div className="absolute inset-0 bg-white/50 backdrop-blur-sm z-10"></div>}
        {appointments.length === 0 && !isLoading ? (
          <div className="bg-white rounded-2xl shadow-xl p-12 border border-gray-200 text-center">
            <AlertCircle className="w-16 h-16 text-gray-300 mx-auto mb-4" />
            <h3 className="text-xl font-bold text-gray-900 mb-2">Không có lịch khám</h3>
            <p className="text-gray-500">Không tìm thấy lịch khám phù hợp với bộ lọc</p>
          </div>
        ) : (
          appointments.map((appointment) => (
            <div
              key={appointment.id}
              className="bg-white rounded-2xl shadow-xl p-6 border border-gray-200 hover:border-emerald-300 transition-all duration-200 cursor-pointer"
              onClick={() => handleViewPatient(appointment)}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-4 flex-1">
                  <div className="w-14 h-14 rounded-full bg-gradient-to-br from-emerald-400 to-teal-500 flex items-center justify-center text-white font-bold text-lg shadow-lg">
                    {appointment.patient?.name?.[0] || 'P'}
                  </div>
                  <div className="flex-1">
                    <h4 className="text-lg font-bold text-gray-900 mb-1">
                      {appointment.patient?.name || 'Bệnh nhân'}
                    </h4>
                    <div className="flex items-center space-x-4 text-sm text-gray-600">
                      <div className="flex items-center space-x-1">
                        <Clock className="w-4 h-4" />
                        <span>{formatTime(appointment.date)}</span>
                      </div>
                      {appointment.patient?.age && (
                        <span>{appointment.patient.age} tuổi</span>
                      )}
                    </div>
                  </div>
                </div>
                <div className="flex items-center space-x-4">
                  {getStatusBadge(appointment.status)}
                  {(appointment.status === 'Scheduled' || appointment.status === 'InProgress' || appointment.status === 'Hospitalized') && (
                    <button onClick={(e) => { e.stopPropagation(); handleStartExamination(appointment.id, appointment.status); }} className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors font-semibold flex items-center space-x-2">
                      <Stethoscope className="w-4 h-4" />
                      <span>{appointment.status === 'InProgress' || appointment.status === 'Hospitalized' ? 'Tiếp tục khám' : 'Bắt đầu khám'}</span>
                    </button>
                  )}
                  {appointment.status === 'Completed' && (
                    <button onClick={(e) => { e.stopPropagation(); handleStartExamination(appointment.id, appointment.status); }} className="px-4 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600 transition-colors font-semibold flex items-center space-x-2">
                      <Eye className="w-4 h-4" />
                      <span>Xem hồ sơ</span>
                    </button>
                  )}
                  <button onClick={(e) => { e.stopPropagation(); handleViewPatient(appointment); }} className="p-2 bg-emerald-100 text-emerald-600 rounded-lg hover:bg-emerald-200 transition-colors">
                    <Eye className="w-5 h-5" />
                  </button>
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
          <div className="flex items-center justify-between">
            <div className="text-sm text-gray-600">
              Trang <span className="font-bold text-emerald-600">{currentPage}</span> / {totalPages}
              <span className="ml-4">
                Tổng <span className="font-bold text-emerald-600">{totalCount}</span> lịch khám
              </span>
            </div>
            <div className="flex items-center space-x-2">
              <button onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))} disabled={currentPage === 1} className={`px-4 py-2 rounded-xl font-semibold transition-colors ${currentPage === 1 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-emerald-500 text-white hover:bg-emerald-600'}`}>
                Trang trước
              </button>
              <div className="flex items-center space-x-1">
                {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                  let pageNum;
                  if (totalPages <= 5) {
                    pageNum = i + 1;
                  } else if (currentPage <= 3) {
                    pageNum = i + 1;
                  } else if (currentPage >= totalPages - 2) {
                    pageNum = totalPages - 4 + i;
                  } else {
                    pageNum = currentPage - 2 + i;
                  }
                  
                  return (
                    <button key={pageNum} onClick={() => setCurrentPage(pageNum)} className={`w-10 h-10 rounded-lg font-semibold transition-colors ${currentPage === pageNum ? 'bg-emerald-500 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
                      {pageNum}
                    </button>
                  );
                })}
              </div>
              <button onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))} disabled={currentPage === totalPages} className={`px-4 py-2 rounded-xl font-semibold transition-colors ${currentPage === totalPages ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-emerald-500 text-white hover:bg-emerald-600'}`}>
                Trang sau
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Patient Detail Modal */}
      {showPatientModal && selectedAppointment && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
            <div className="sticky top-0 bg-gradient-to-r from-emerald-500 to-teal-500 text-white p-6 rounded-t-2xl flex items-center justify-between">
              <h3 className="text-2xl font-bold">Thông tin bệnh nhân</h3>
              <button onClick={() => setShowPatientModal(false)} className="p-2 hover:bg-white/20 rounded-lg transition-colors">
                <X className="w-6 h-6" />
              </button>
            </div>
            <div className="p-6 space-y-6">
              <div>
                <h4 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
                  <User className="w-5 h-5 text-emerald-600" />
                  <span>Thông tin cơ bản</span>
                </h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Họ tên</p><p className="text-sm font-semibold text-gray-900">{selectedAppointment.patient?.name || 'N/A'}</p></div>
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Tuổi</p><p className="text-sm font-semibold text-gray-900">{selectedAppointment.patient?.age || 'N/A'}</p></div>
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Email</p><p className="text-sm font-semibold text-gray-900">{selectedAppointment.patient?.email || 'N/A'}</p></div>
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Mã BN</p><p className="text-sm font-semibold text-gray-900">BN{selectedAppointment.patientId?.toString().padStart(5, '0')}</p></div>
                </div>
              </div>
              <div>
                <h4 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
                  <Calendar className="w-5 h-5 text-emerald-600" />
                  <span>Thông tin lịch khám</span>
                </h4>
                <div className="grid grid-cols-2 gap-4">
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Ngày khám</p><p className="text-sm font-semibold text-gray-900">{new Date(selectedAppointment.date).toLocaleDateString('vi-VN')}</p></div>
                  <div className="p-4 bg-gray-50 rounded-xl"><p className="text-xs text-gray-500 mb-1">Giờ khám</p><p className="text-sm font-semibold text-gray-900">{formatTime(selectedAppointment.date)}</p></div>
                  <div className="p-4 bg-gray-50 rounded-xl col-span-2"><p className="text-xs text-gray-500 mb-1">Trạng thái</p><div className="mt-2">{getStatusBadge(selectedAppointment.status)}</div></div>
                </div>
              </div>
            </div>
            <div className="sticky bottom-0 bg-gray-50 p-6 rounded-b-2xl flex justify-end space-x-3">
              <button onClick={() => setShowPatientModal(false)} className="px-6 py-2 bg-gray-200 text-gray-700 rounded-xl hover:bg-gray-300 transition-colors font-semibold">Đóng</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}