'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import toast from 'react-hot-toast';
import { authApi, notificationApi, doctorApi } from '@/lib/api'; // Add doctorApi
import { User, Doctor } from '@/lib/types';
import {
  User as UserIcon,
  Calendar,
  ClipboardList,
  Activity,
  Settings,
  LogOut,
  Stethoscope,
  Clock,
  Users,
  FileText,
  TrendingUp,
  Bell,
  Video, // Add Video icon
} from 'lucide-react';

// Import sections
import CheckInSection from '@/components/doctor/CheckInSection';
import NotificationsSection from '@/components/doctor/NotificationsSection';
import DoctorProfileSection from '@/components/doctor/DoctorProfileSection';
import AppointmentsSection from '@/components/doctor/AppointmentsSection';
import DoctorOnlineConsultations from '@/components/doctor/DoctorOnlineConsultations';
import { useAuth } from '@/hooks/useAuth'; // Import useAuth

type SectionType = 'checkin' | 'notifications' | 'profile' | 'online_consultations' | 'appointments' | 'consultation' | 'patients' | 'statistics' | 'settings';

const sidebarItems = [
  { id: 'checkin' as SectionType, name: 'Check-in / Check-out', icon: Clock, color: 'bg-green-500' },
  { id: 'notifications' as SectionType, name: 'Thông báo', icon: Bell, color: 'bg-amber-500' },
  { id: 'profile' as SectionType, name: 'Thông tin bác sĩ', icon: UserIcon, color: 'bg-emerald-500' },
  { id: 'online_consultations' as SectionType, name: 'Tư vấn trực tuyến', icon: Video, color: 'bg-indigo-500' }, // New item
  { id: 'appointments' as SectionType, name: 'Lịch khám', icon: Calendar, color: 'bg-blue-500' },
  { id: 'consultation' as SectionType, name: 'Khám bệnh', icon: Stethoscope, color: 'bg-purple-500' },
  { id: 'patients' as SectionType, name: 'Bệnh nhân', icon: Users, color: 'bg-cyan-500' },
  { id: 'statistics' as SectionType, name: 'Thống kê', icon: TrendingUp, color: 'bg-orange-500' },
  { id: 'settings' as SectionType, name: 'Cài đặt', icon: Settings, color: 'bg-gray-500' },
];

export default function DoctorPortal() {
  const router = useRouter();
  const { user, isLoading: isAuthLoading } = useAuth(); // Use useAuth hook
  const [doctor, setDoctor] = useState<Doctor | null>(null);
  const [activeSection, setActiveSection] = useState<SectionType>('checkin');
  const [isLoading, setIsLoading] = useState(true); // Keep for doctor details independent of auth
  const [isOnDuty, setIsOnDuty] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);

  // Refetch doctor data when user from useAuth is loaded and not null
  useEffect(() => {
    if (user && user.doctorId && !isAuthLoading) {
      fetchDoctorData(user.doctorId);
    } else if (!user && !isAuthLoading) {
      toast.error('Bạn cần đăng nhập để truy cập trang này!');
      router.push('/');
    }
  }, [user, isAuthLoading, router]);

  // Original fetchUserData logic, adapted for doctor-specific data
  const fetchDoctorData = async (doctorId: number) => {
    setIsLoading(true);
    try {
      // Use doctorApi to fetch doctor details
      const response = await doctorApi.getById(doctorId);
      setDoctor(response.data); // Assuming response.data is the Doctor object
    } catch (error) {
      console.error('Error fetching doctor data:', error);
      toast.error('Không thể tải dữ liệu bác sĩ!');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    // fetchUserData(); // Remove this, rely on useAuth
    checkDutyStatus();
    fetchUnreadCount();
    
    // Poll for unread notifications every 30 seconds
    const interval = setInterval(fetchUnreadCount, 30000);
    return () => clearInterval(interval);
  }, []); // Dependency array might need adjustment

  const fetchUnreadCount = async () => {
    try {
      const response = await notificationApi.getUnread();
      setUnreadCount(response.data.count);
    } catch (error) {
      console.error('Error fetching unread count:', error);
    }
  };

  // fetchUserData removed, now handled by useAuth for user and fetchDoctorData for doctor specific info

  const checkDutyStatus = () => {
    setIsOnDuty(true);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    // useAuth.logout() would be better here if it were a Context Provider
    // For now, direct localStorage clear is fine for this example
    router.push('/');
    toast.success('Đăng xuất thành công!');
  };

  const toggleDutyStatus = () => {
    setIsOnDuty(!isOnDuty);
    toast.success(isOnDuty ? 'Đã kết thúc ca trực' : 'Đã bắt đầu ca trực');
  };

  const renderSection = () => {
    if (!user) return null; // Wait for user data from useAuth

    if (activeSection === 'checkin') {
      return <CheckInSection onStatusChange={setIsOnDuty} />;
    }
    
    if (activeSection === 'notifications') {
      return <NotificationsSection onCountChange={setUnreadCount} />;
    }
    
    if (activeSection === 'profile') {
      return <DoctorProfileSection />;
    }

    if (activeSection === 'online_consultations') {
      if (!user.doctorId) {
        return <div className="bg-white rounded-2xl shadow-xl p-8 border border-emerald-100 text-red-500">
                 <h3 className="text-2xl font-bold text-gray-900 mb-4">Lỗi</h3>
                 <p>Không tìm thấy ID bác sĩ cho người dùng này. Vui lòng kiểm tra lại tài khoản.</p>
               </div>;
      }
      return <DoctorOnlineConsultations doctorId={user.doctorId} />;
    }
    
    if (activeSection === 'appointments') {
      return <AppointmentsSection />;
    }
    
    return (
      <div className="bg-white rounded-2xl shadow-xl p-8 border border-emerald-100">
        <h3 className="text-2xl font-bold text-gray-900 mb-4">
          {sidebarItems.find(item => item.id === activeSection)?.name}
        </h3>
        <p className="text-gray-600">Content coming soon...</p>
      </div>
    );
  };

  if (isAuthLoading || isLoading) { // Combine loading states
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-emerald-50 via-teal-50 to-cyan-100">
        <div className="relative">
          <div className="animate-spin rounded-full h-16 w-16 border-b-2 border-t-2 border-emerald-600"></div>
          <Stethoscope className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-6 h-6 text-emerald-600 animate-pulse" />
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-emerald-50 via-teal-50 to-cyan-100">
      <div className="flex h-screen">
        {/* Sidebar */}
        <aside className="w-72 bg-white shadow-2xl border-r border-emerald-100">
          {/* Header */}
          <div className="p-6 border-b border-emerald-100 bg-gradient-to-r from-emerald-500 to-teal-500">
            <div className="flex items-center space-x-3">
              <div className="p-3 bg-white rounded-xl shadow-lg">
                <Stethoscope className="w-6 h-6 text-emerald-600" />
              </div>
              <div>
                <h1 className="text-2xl font-bold text-white">HMS Doctor</h1>
                <p className="text-xs text-emerald-100 mt-0.5">Portal quản lý</p>
              </div>
            </div>
          </div>

          {/* User Info */}
          <div className="p-5 border-b border-gray-200 bg-gradient-to-br from-white to-emerald-50">
            <div className="flex items-center space-x-3">
              <div className="relative">
                <div className="w-14 h-14 rounded-full bg-gradient-to-br from-emerald-400 to-teal-500 flex items-center justify-center text-white font-bold text-lg shadow-lg ring-4 ring-emerald-100">
                  {user?.firstName?.[0]}{user?.lastName?.[0]}
                </div>
                <div className={`absolute -bottom-1 -right-1 w-5 h-5 rounded-full border-2 border-white shadow-md ${
                  isOnDuty ? 'bg-green-500 animate-pulse' : 'bg-gray-400'
                }`}></div>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-bold text-gray-900 truncate">
                  BS. {user?.firstName} {user?.lastName}
                </p>
                <p className="text-xs text-emerald-600 truncate font-medium">Chuyên khoa nội</p>
                <div className="flex items-center space-x-1 mt-1">
                  <Clock className="w-3 h-3 text-gray-400" />
                  <p className="text-xs text-gray-500">{isOnDuty ? 'Đang trực' : 'Ngoài giờ'}</p>
                </div>
              </div>
              <button
                onClick={toggleDutyStatus}
                className={`p-2 rounded-lg transition-all duration-300 ${
                  isOnDuty 
                    ? 'bg-green-100 text-green-600 hover:bg-green-200' 
                    : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
                }`}
              >
                <Activity className={`w-5 h-5 ${isOnDuty ? 'animate-pulse' : ''}`} />
              </button>
            </div>

            {/* Quick Stats */}
            <div className="grid grid-cols-3 gap-2 mt-4">
              <div className="bg-white rounded-lg p-3 text-center shadow-sm border border-emerald-100 hover:shadow-md transition-shadow">
                <Calendar className="w-4 h-4 text-blue-500 mx-auto mb-1" />
                <p className="text-xs text-gray-500">Hôm nay</p>
                <p className="text-lg font-bold text-gray-900">12</p>
              </div>
              <div className="bg-white rounded-lg p-3 text-center shadow-sm border border-emerald-100 hover:shadow-md transition-shadow">
                <Users className="w-4 h-4 text-purple-500 mx-auto mb-1" />
                <p className="text-xs text-gray-500">Đang chờ</p>
                <p className="text-lg font-bold text-gray-900">5</p>
              </div>
              <div className="bg-white rounded-lg p-3 text-center shadow-sm border border-emerald-100 hover:shadow-md transition-shadow">
                <FileText className="w-4 h-4 text-orange-500 mx-auto mb-1" />
                <p className="text-xs text-gray-500">Hoàn thành</p>
                <p className="text-lg font-bold text-gray-900">7</p>
              </div>
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
            {sidebarItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeSection === item.id;
              const showBadge = item.id === 'notifications' && unreadCount > 0;
              
              return (
                <button
                  key={item.id}
                  onClick={() => setActiveSection(item.id)}
                  className={`w-full flex items-center space-x-3 px-4 py-3.5 rounded-xl transition-all duration-300 group relative ${
                    isActive
                      ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-lg shadow-emerald-200 scale-105'
                      : 'text-gray-600 hover:bg-gray-50 hover:scale-102'
                  }`}
                >
                  <div className={`p-2.5 rounded-lg transition-all duration-300 ${
                    isActive 
                      ? 'bg-white/20 shadow-inner' 
                      : `${item.color} bg-opacity-10 group-hover:bg-opacity-20`
                  }`}>
                    <Icon className={`w-5 h-5 ${
                      isActive 
                        ? 'text-white' 
                        : `${item.color.replace('bg-', 'text-')} group-hover:scale-110 transition-transform`
                    }`} />
                  </div>
                  <span className="font-semibold text-sm flex-1 text-left">{item.name}</span>
                  {showBadge && (
                    <span className="bg-red-500 text-white text-xs font-bold px-2 py-1 rounded-full min-w-[20px] text-center animate-pulse">
                      {unreadCount > 99 ? '99+' : unreadCount}
                    </span>
                  )}
                  {isActive && !showBadge && (
                    <div className="w-2 h-2 rounded-full bg-white animate-pulse"></div>
                  )}
                </button>
              );
            })}
          </nav>

          {/* Logout */}
          <div className="p-4 border-t border-gray-200">
            <button
              onClick={handleLogout}
              className="w-full flex items-center space-x-3 px-4 py-3 rounded-xl text-red-600 hover:bg-red-50 transition-all duration-200 group"
            >
              <div className="p-2 rounded-lg bg-red-50 group-hover:bg-red-100 transition-colors">
                <LogOut className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
              </div>
              <span className="font-medium text-sm">Đăng xuất</span>
            </button>
          </div>
        </aside>

        {/* Main Content */}
        <main className="flex-1 overflow-y-auto">
          <div className="p-8">
            <div className="mb-8">
              <div className="flex items-center space-x-2 text-sm text-gray-500 mb-3">
                <span>Trang chủ</span>
                <span>/</span>
                <span className="text-emerald-600 font-medium">
                  {sidebarItems.find(item => item.id === activeSection)?.name}
                </span>
              </div>
              <h2 className="text-3xl font-bold text-gray-900">
                {sidebarItems.find(item => item.id === activeSection)?.name}
              </h2>
            </div>
            <div className="animate-fadeIn">
              {renderSection()}
            </div>
          </div>
        </main>
      </div>

      <style jsx>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(10px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .animate-fadeIn {
          animation: fadeIn 0.3s ease-out;
        }
      `}</style>
    </div>
  );
}
