'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { doctorAttendanceApi } from '@/lib/api';
import { CheckInStatus, DoctorAttendance } from '@/lib/types';
import {
  Clock,
  CheckCircle,
  AlertCircle,
  Calendar,
  Timer,
  Sparkles,
  LogIn,
  Activity,
  LogOut,
} from 'lucide-react';

interface CheckInSectionProps {
  onStatusChange?: (isCheckedIn: boolean) => void;
}

export default function CheckInSection({ onStatusChange }: CheckInSectionProps) {
  const [checkInStatus, setCheckInStatus] = useState<CheckInStatus | null>(null);
  const [todayAttendance, setTodayAttendance] = useState<DoctorAttendance | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isCheckingIn, setIsCheckingIn] = useState(false);
  const [isCheckingOut, setIsCheckingOut] = useState(false);
  const [currentTime, setCurrentTime] = useState(new Date());
  const [note, setNote] = useState('');

  useEffect(() => {
    fetchCheckInStatus();
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const fetchCheckInStatus = async () => {
    setIsLoading(true);
    try {
      const response = await doctorAttendanceApi.getCheckInStatus();
      setCheckInStatus(response.data);
      
      if (response.data.isCheckedIn) {
        const attendanceResponse = await doctorAttendanceApi.getTodayAttendance();
        setTodayAttendance(attendanceResponse.data);
      }
      
      onStatusChange?.(response.data.isCheckedIn);
    } catch (error: any) {
      console.error('Error fetching check-in status:', error);
      toast.error(error.response?.data?.message || 'Không thể tải trạng thái');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCheckIn = async () => {
    setIsCheckingIn(true);
    try {
      const response = await doctorAttendanceApi.checkIn(note);
      toast.success(response.data.message || 'Check-in thành công!');
      setNote('');
      await fetchCheckInStatus();
    } catch (error: any) {
      console.error('Error checking in:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi check-in');
    } finally {
      setIsCheckingIn(false);
    }
  };

  const handleCheckOut = async () => {
    if (!confirm('Bạn có chắc muốn check-out?')) return;

    setIsCheckingOut(true);
    try {
      const response = await doctorAttendanceApi.checkOut(note);
      toast.success(response.data.message || 'Check-out thành công!');
      setNote('');
      await fetchCheckInStatus();
    } catch (error: any) {
      console.error('Error checking out:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi check-out');
    } finally {
      setIsCheckingOut(false);
    }
  };

  const formatTime = (dateString?: string) => {
    if (!dateString) return '--:--';
    
    try {
      // Parse UTC time from backend
      const utcDate = new Date(dateString);
      
      // Convert to VN timezone (UTC+7)
      const vnOffset = 7 * 60; // 7 hours in minutes
      const localOffset = utcDate.getTimezoneOffset(); // Local offset in minutes
      const vnTime = new Date(utcDate.getTime() + (vnOffset + localOffset) * 60000);
      
      // Format as HH:mm
      const hours = vnTime.getHours().toString().padStart(2, '0');
      const minutes = vnTime.getMinutes().toString().padStart(2, '0');
      
      return `${hours}:${minutes}`;
    } catch (error) {
      console.error('Error formatting time:', error);
      return '--:--';
    }
  };

  const formatShiftTime = (timeString?: string) => {
    if (!timeString) return '--:--';
    return timeString.substring(0, 5);
  };

  if (isLoading) {
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
      {/* Current Time Card */}
      <div className="bg-gradient-to-br from-emerald-500 to-teal-500 rounded-2xl shadow-xl p-8 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <div className="p-4 bg-white/20 rounded-xl backdrop-blur-sm">
              <Clock className="w-8 h-8" />
            </div>
            <div>
              <p className="text-emerald-100 text-sm font-medium">Thời gian hiện tại</p>
              <h2 className="text-4xl font-bold mt-1">
                {currentTime.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
              </h2>
              <p className="text-emerald-100 text-sm mt-1">
                {currentTime.toLocaleDateString('vi-VN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
              </p>
            </div>
          </div>
          
          {checkInStatus?.isCheckedIn && (
            <div className="flex items-center space-x-2 bg-green-500/30 backdrop-blur-sm px-4 py-2 rounded-xl border border-white/20">
              <Activity className="w-5 h-5 animate-pulse" />
              <span className="font-semibold">Đang làm việc</span>
            </div>
          )}
        </div>
      </div>

      {/* Shift Information */}
      {checkInStatus?.allShifts && checkInStatus.allShifts.length > 0 && (
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
          <div className="flex items-center space-x-3 mb-4">
            <div className="p-3 bg-blue-100 rounded-xl">
              <Calendar className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-900">Ca làm việc hôm nay</h3>
              <p className="text-sm text-gray-500">
                {checkInStatus.allShifts.length} ca trực - Thông tin ca làm việc
              </p>
            </div>
          </div>
          
          <div className="space-y-3">
            {checkInStatus.allShifts.map((shift, index) => {
              const isCurrentShift = shift.id === checkInStatus.shift?.id;
              const shiftStart = formatShiftTime(shift.startTime);
              const shiftEnd = formatShiftTime(shift.endTime);
              
              return (
                <div
                  key={shift.id}
                  className={`p-4 rounded-xl border-2 transition-all ${
                    isCurrentShift
                      ? 'border-emerald-500 bg-emerald-50'
                      : 'border-gray-200 bg-gray-50'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-3">
                      <div className={`p-2 rounded-lg ${
                        isCurrentShift ? 'bg-emerald-500' : 'bg-gray-400'
                      }`}>
                        <Clock className="w-4 h-4 text-white" />
                      </div>
                      <div>
                        <p className={`text-sm font-medium ${
                          isCurrentShift ? 'text-emerald-900' : 'text-gray-700'
                        }`}>
                          Ca {index + 1}
                        </p>
                        <p className={`text-lg font-bold ${
                          isCurrentShift ? 'text-emerald-600' : 'text-gray-900'
                        }`}>
                          {shiftStart} - {shiftEnd}
                        </p>
                      </div>
                    </div>
                    {isCurrentShift && (
                      <span className="px-3 py-1 bg-emerald-500 text-white text-xs font-bold rounded-full">
                        Ca hiện tại
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {checkInStatus.earliestCheckInTime && !checkInStatus.isCheckedIn && (
            <div className="mt-4 p-4 bg-amber-50 border border-amber-200 rounded-xl flex items-start space-x-3">
              <Timer className="w-5 h-5 text-amber-600 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-amber-900">
                  Có thể check-in từ: <span className="font-bold">{formatTime(checkInStatus.earliestCheckInTime)}</span>
                </p>
                <p className="text-xs text-amber-700 mt-1">
                  (10 phút trước giờ bắt đầu ca)
                </p>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Check-in Status */}
      {!checkInStatus?.shift && (
        <div className="bg-white rounded-2xl shadow-xl p-8 border border-gray-200">
          <div className="text-center py-8">
            <div className="p-4 bg-gray-100 rounded-full w-20 h-20 mx-auto mb-4 flex items-center justify-center">
              <AlertCircle className="w-10 h-10 text-gray-400" />
            </div>
            <h3 className="text-xl font-bold text-gray-900 mb-2">Không có ca làm việc</h3>
            <p className="text-gray-500">Bạn không có ca làm việc được xếp lịch cho hôm nay</p>
          </div>
        </div>
      )}

      {/* Shift Completed */}
      {checkInStatus?.isCompleted && (
        <div className="bg-white rounded-2xl shadow-xl p-8 border border-green-200">
          <div className="text-center py-8">
            <div className="p-4 bg-green-100 rounded-full w-20 h-20 mx-auto mb-4 flex items-center justify-center">
              <CheckCircle className="w-10 h-10 text-green-600" />
            </div>
            <h3 className="text-xl font-bold text-gray-900 mb-2">Đã hoàn thành ca làm việc</h3>
            <p className="text-gray-500">Bạn đã hoàn thành ca làm việc hôm nay</p>
            
            {checkInStatus.attendance && (
              <div className="mt-6 space-y-3 max-w-md mx-auto">
                <div className="flex items-center justify-between p-4 bg-green-50 rounded-xl">
                  <span className="text-sm font-medium text-gray-700">Giờ check-in</span>
                  <span className="text-lg font-bold text-green-600">
                    {formatTime(checkInStatus.attendance.checkInTime)}
                  </span>
                </div>
                <div className="flex items-center justify-between p-4 bg-gray-50 rounded-xl">
                  <span className="text-sm font-medium text-gray-700">Giờ check-out</span>
                  <span className="text-lg font-bold text-gray-600">
                    {formatTime(checkInStatus.attendance.checkOutTime)}
                  </span>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Check-in Action */}
      {checkInStatus?.canCheckIn && !checkInStatus.isCheckedIn && !checkInStatus.isCompleted && (
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
          <div className="flex items-center space-x-3 mb-4">
            <div className="p-3 bg-emerald-100 rounded-xl">
              <LogIn className="w-6 h-6 text-emerald-600" />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-900">Check-in</h3>
              <p className="text-sm text-gray-500">Bắt đầu ca làm việc</p>
            </div>
          </div>

          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Ghi chú (tùy chọn)
              </label>
              <textarea
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Nhập ghi chú nếu cần..."
                className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all"
                rows={3}
              />
            </div>

            <button
              onClick={handleCheckIn}
              disabled={isCheckingIn}
              className="w-full bg-gradient-to-r from-emerald-500 to-teal-500 text-white font-bold py-4 px-6 rounded-xl hover:from-emerald-600 hover:to-teal-600 transition-all duration-300 shadow-lg hover:shadow-xl disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center space-x-2"
            >
              {isCheckingIn ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>
                  <span>Đang check-in...</span>
                </>
              ) : (
                <>
                  <CheckCircle className="w-5 h-5" />
                  <span>Check-in ngay</span>
                </>
              )}
            </button>
          </div>
        </div>
      )}

      {/* Already Checked In */}
      {checkInStatus?.isCheckedIn && todayAttendance && (
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-green-200">
          <div className="flex items-center space-x-3 mb-6">
            <div className="p-3 bg-green-100 rounded-xl">
              <CheckCircle className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-900">Đã check-in</h3>
              <p className="text-sm text-gray-500">Bạn đã bắt đầu ca làm việc</p>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between p-4 bg-green-50 rounded-xl">
              <span className="text-sm font-medium text-gray-700">Giờ check-in</span>
              <span className="text-lg font-bold text-green-600">
                {formatTime(todayAttendance.checkInTime)}
              </span>
            </div>

            {todayAttendance.checkInNote && (
              <div className="p-4 bg-gray-50 rounded-xl">
                <p className="text-xs text-gray-500 mb-1">Ghi chú check-in</p>
                <p className="text-sm text-gray-900">{todayAttendance.checkInNote}</p>
              </div>
            )}

            <div className="flex items-center space-x-2 p-4 bg-gradient-to-r from-green-50 to-emerald-50 rounded-xl border border-green-200">
              <Sparkles className="w-5 h-5 text-green-600 animate-pulse" />
              <p className="text-sm font-medium text-green-800">
                Chúc bạn một ngày làm việc vui vẻ!
              </p>
            </div>

            {/* Checkout section */}
            {!todayAttendance.checkOutTime && (
              <>
                {checkInStatus.canCheckOut ? (
                  <div className="pt-4 border-t border-gray-200">
                    <div className="flex items-center space-x-3 mb-4">
                      <div className="p-3 bg-red-100 rounded-xl">
                        <LogOut className="w-6 h-6 text-red-600" />
                      </div>
                      <div>
                        <h4 className="text-lg font-bold text-gray-900">Check-out</h4>
                        <p className="text-sm text-gray-500">Kết thúc ca làm việc</p>
                      </div>
                    </div>

                    <div className="space-y-4">
                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-2">
                          Ghi chú (tùy chọn)
                        </label>
                        <textarea
                          value={note}
                          onChange={(e) => setNote(e.target.value)}
                          placeholder="Nhập ghi chú nếu cần..."
                          className="w-full px-4 py-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-red-500 transition-all"
                          rows={3}
                        />
                      </div>

                      <button
                        onClick={handleCheckOut}
                        disabled={isCheckingOut}
                        className="w-full bg-gradient-to-r from-red-500 to-red-600 text-white font-bold py-4 px-6 rounded-xl hover:from-red-600 hover:to-red-700 transition-all duration-300 shadow-lg hover:shadow-xl disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center space-x-2"
                      >
                        {isCheckingOut ? (
                          <>
                            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>
                            <span>Đang check-out...</span>
                          </>
                        ) : (
                          <>
                            <LogOut className="w-5 h-5" />
                            <span>Check-out ngay</span>
                          </>
                        )}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="pt-4 border-t border-gray-200">
                    <div className="p-4 bg-amber-50 border border-amber-200 rounded-xl flex items-start space-x-3">
                      <AlertCircle className="w-5 h-5 text-amber-600 mt-0.5" />
                      <div>
                        <p className="text-sm font-medium text-amber-900">
                          Chưa thể check-out
                        </p>
                        <p className="text-xs text-amber-700 mt-1">
                          Chỉ có thể check-out sau khi ca làm việc kết thúc và không còn bệnh nhân đang khám
                        </p>
                      </div>
                    </div>
                  </div>
                )}
              </>
            )}

            {/* Already checked out */}
            {todayAttendance.checkOutTime && (
              <div className="pt-4 border-t border-gray-200">
                <div className="flex items-center justify-between p-4 bg-gray-50 rounded-xl">
                  <span className="text-sm font-medium text-gray-700">Giờ check-out</span>
                  <span className="text-lg font-bold text-gray-600">
                    {formatTime(todayAttendance.checkOutTime)}
                  </span>
                </div>

                {todayAttendance.checkOutNote && (
                  <div className="p-4 bg-gray-50 rounded-xl mt-3">
                    <p className="text-xs text-gray-500 mb-1">Ghi chú check-out</p>
                    <p className="text-sm text-gray-900">{todayAttendance.checkOutNote}</p>
                  </div>
                )}

                <div className="flex items-center space-x-2 p-4 bg-gray-100 rounded-xl border border-gray-200 mt-3">
                  <CheckCircle className="w-5 h-5 text-gray-600" />
                  <p className="text-sm font-medium text-gray-700">
                    Đã hoàn thành ca làm việc!
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Cannot Check-in Yet */}
      {!checkInStatus?.canCheckIn && !checkInStatus?.isCheckedIn && checkInStatus?.shift && !checkInStatus?.isCompleted && (
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-amber-200">
          <div className="flex items-center space-x-3 mb-4">
            <div className="p-3 bg-amber-100 rounded-xl">
              <AlertCircle className="w-6 h-6 text-amber-600" />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-900">Chưa thể check-in</h3>
              <p className="text-sm text-gray-500">{checkInStatus.message}</p>
            </div>
          </div>

          {checkInStatus.earliestCheckInTime && (
            <div className="p-4 bg-amber-50 rounded-xl">
              <p className="text-sm text-amber-900">
                Vui lòng đợi đến{' '}
                <span className="font-bold">{formatTime(checkInStatus.earliestCheckInTime)}</span>
                {' '}để check-in
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
