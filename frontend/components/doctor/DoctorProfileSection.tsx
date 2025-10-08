'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { authApi, doctorApi } from '@/lib/api';
import { User, Doctor } from '@/lib/types';
import {
  User as UserIcon,
  Mail,
  Phone,
  MapPin,
  Calendar,
  Activity,
  Briefcase,
  Award,
  Clock,
  Shield,
  Lock,
} from 'lucide-react';

export default function DoctorProfileSection() {
  const [user, setUser] = useState<User | null>(null);
  const [doctor, setDoctor] = useState<Doctor | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetchProfile();
  }, []);

  const fetchProfile = async () => {
    setIsLoading(true);
    try {
      const response = await authApi.getProfile();
      const userData = response.data;
      
      setUser({
        id: userData.id,
        username: userData.username,
        email: userData.email,
        firstName: userData.firstName,
        lastName: userData.lastName,
        role: userData.role,
        isActive: true,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        patientId: userData.patientId,
        doctorId: userData.doctorId,
      });

      // Fetch doctor details from API
      if (userData.doctorId) {
        try {
          const doctorResponse = await doctorApi.getById(userData.doctorId);
          setDoctor(doctorResponse.data);
        } catch (doctorError: any) {
          console.error('Error fetching doctor details:', doctorError);
          toast.error('Không thể tải thông tin bác sĩ!');
        }
      }
    } catch (error: any) {
      console.error('Error fetching profile:', error);
      toast.error('Không thể tải thông tin!');
    } finally {
      setIsLoading(false);
    }
  };

  const getStatusText = () => {
    if (!doctor) return 'Không xác định';
    
    const statuses = [];
    if (doctor.isActive) statuses.push('Hoạt động');
    if (doctor.isOnDuty) statuses.push('Đang trực');
    if (doctor.isOffDuty) statuses.push('Sẵn sàng');
    if (doctor.isOnLeave) statuses.push('Nghỉ phép');
    if (doctor.isOnCall) statuses.push('Trực chờ');
    if (doctor.isInSurgery) statuses.push('Đang phẫu thuật');
    if (doctor.isOnVacation) statuses.push('Nghỉ dài hạn');
    
    return statuses.length > 0 ? statuses.join(', ') : 'Không xác định';
  };

  const getStatusColor = () => {
    if (!doctor) return 'bg-gray-100 text-gray-600';
    
    if (doctor.isInSurgery) return 'bg-red-100 text-red-700';
    if (doctor.isOnDuty) return 'bg-blue-100 text-blue-700';
    if (doctor.isOffDuty) return 'bg-green-100 text-green-700';
    if (doctor.isOnLeave || doctor.isOnVacation) return 'bg-orange-100 text-orange-700';
    if (doctor.isOnCall) return 'bg-purple-100 text-purple-700';
    if (doctor.isActive) return 'bg-emerald-100 text-emerald-700';
    
    return 'bg-gray-100 text-gray-600';
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
      {/* Profile Header */}
      <div className="bg-gradient-to-br from-emerald-500 to-teal-500 rounded-2xl shadow-xl p-8 text-white">
        <div className="flex items-start justify-between">
          <div className="flex items-center space-x-6">
            <div className="relative">
              <div className="w-24 h-24 rounded-full bg-white/20 backdrop-blur-sm flex items-center justify-center text-white font-bold text-3xl shadow-lg ring-4 ring-white/30">
                {user?.firstName?.[0]}{user?.lastName?.[0]}
              </div>
              <div className={`absolute -bottom-2 -right-2 w-8 h-8 rounded-full border-4 border-white shadow-lg ${
                doctor?.isActive ? 'bg-green-500 animate-pulse' : 'bg-gray-400'
              }`}></div>
            </div>
            <div>
              <h2 className="text-3xl font-bold mb-2">
                BS. {user?.firstName} {user?.lastName}
              </h2>
              <p className="text-emerald-100 text-lg mb-3">{doctor?.specialty || 'Chuyên khoa'}</p>
              <div className={`inline-flex items-center space-x-2 px-4 py-2 rounded-full text-sm font-semibold ${getStatusColor()} bg-white/90`}>
                <Activity className="w-4 h-4" />
                <span>{getStatusText()}</span>
              </div>
            </div>
          </div>
          
          <button className="flex items-center space-x-2 px-4 py-2 bg-white/20 hover:bg-white/30 backdrop-blur-sm rounded-xl transition-all duration-200 border border-white/30">
            <Lock className="w-4 h-4" />
            <span className="text-sm font-semibold">Đổi mật khẩu</span>
          </button>
        </div>
      </div>

      {/* Profile Details */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Personal Information */}
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
          <div className="flex items-center space-x-3 mb-6">
            <div className="p-3 bg-blue-100 rounded-xl">
              <UserIcon className="w-6 h-6 text-blue-600" />
            </div>
            <h3 className="text-xl font-bold text-gray-900">Thông tin cá nhân</h3>
          </div>

          <div className="space-y-4">
            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <Mail className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Email</p>
                <p className="text-sm font-semibold text-gray-900">{user?.email || 'Chưa cập nhật'}</p>
              </div>
            </div>

            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <UserIcon className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Tên đăng nhập</p>
                <p className="text-sm font-semibold text-gray-900">{user?.username || 'Chưa cập nhật'}</p>
              </div>
            </div>

            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <Shield className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Vai trò</p>
                <p className="text-sm font-semibold text-gray-900">{user?.role || 'Bác sĩ'}</p>
              </div>
            </div>

            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <Briefcase className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Mã bác sĩ</p>
                <p className="text-sm font-semibold text-gray-900">BS{doctor?.id?.toString().padStart(5, '0') || '00000'}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Professional Information */}
        <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
          <div className="flex items-center space-x-3 mb-6">
            <div className="p-3 bg-purple-100 rounded-xl">
              <Award className="w-6 h-6 text-purple-600" />
            </div>
            <h3 className="text-xl font-bold text-gray-900">Thông tin chuyên môn</h3>
          </div>

          <div className="space-y-4">
            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <Award className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Chuyên khoa</p>
                <p className="text-sm font-semibold text-gray-900">{doctor?.specialty || 'Chưa cập nhật'}</p>
              </div>
            </div>

            <div className="flex items-start space-x-3 p-4 bg-gray-50 rounded-xl">
              <Clock className="w-5 h-5 text-gray-400 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-gray-500 mb-1">Ngày tham gia</p>
                <p className="text-sm font-semibold text-gray-900">
                  {doctor?.createdAt 
                    ? new Date(doctor.createdAt).toLocaleDateString('vi-VN', { 
                        year: 'numeric', 
                        month: 'long', 
                        day: 'numeric' 
                      })
                    : 'Chưa cập nhật'}
                </p>
              </div>
            </div>

            <div className="flex items-start space-x-3 p-4 bg-emerald-50 rounded-xl border border-emerald-200">
              <Activity className="w-5 h-5 text-emerald-600 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs text-emerald-700 mb-1">Trạng thái hoạt động</p>
                <div className="flex flex-wrap gap-2 mt-2">
                  {doctor?.isActive && (
                    <span className="px-2 py-1 bg-green-100 text-green-700 text-xs font-semibold rounded-lg">✓ Hoạt động</span>
                  )}
                  {doctor?.isOnDuty && (
                    <span className="px-2 py-1 bg-blue-100 text-blue-700 text-xs font-semibold rounded-lg">✓ Đang trực</span>
                  )}
                  {doctor?.isOffDuty && (
                    <span className="px-2 py-1 bg-teal-100 text-teal-700 text-xs font-semibold rounded-lg">✓ Sẵn sàng</span>
                  )}
                  {doctor?.isOnLeave && (
                    <span className="px-2 py-1 bg-orange-100 text-orange-700 text-xs font-semibold rounded-lg">! Nghỉ phép</span>
                  )}
                  {doctor?.isOnCall && (
                    <span className="px-2 py-1 bg-purple-100 text-purple-700 text-xs font-semibold rounded-lg">✓ Trực chờ</span>
                  )}
                  {doctor?.isInSurgery && (
                    <span className="px-2 py-1 bg-red-100 text-red-700 text-xs font-semibold rounded-lg">! Đang phẫu thuật</span>
                  )}
                  {doctor?.isOnVacation && (
                    <span className="px-2 py-1 bg-orange-100 text-orange-700 text-xs font-semibold rounded-lg">! Nghỉ dài hạn</span>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
