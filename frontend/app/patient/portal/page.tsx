'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api, authApi } from '@/lib/api';
import { User, Patient } from '@/lib/types';
import toast from 'react-hot-toast';
import {
  User as UserIcon,
  Calendar,
  FileText,
  Fingerprint,
  Heart,
  Settings,
  LogOut,
  Activity,
} from 'lucide-react';

// Import sections
import ProfileSection from '@/components/patient/ProfileSection';
import AppointmentsSection from '@/components/patient/AppointmentsSection';
import MedicalRecordsSection from '@/components/patient/MedicalRecordsSection';
import IdentifiersSection from '@/components/patient/IdentifiersSection';
import HealthMetricsSection from '@/components/patient/HealthMetricsSection';
import SettingsSection from '@/components/patient/SettingsSection';

type SectionType = 'profile' | 'appointments' | 'records' | 'identifiers' | 'health' | 'settings';

const sidebarItems = [
  { id: 'profile' as SectionType, name: 'Thông tin cá nhân', icon: UserIcon, color: 'bg-blue-500' },
  { id: 'appointments' as SectionType, name: 'Lịch hẹn', icon: Calendar, color: 'bg-green-500' },
  { id: 'records' as SectionType, name: 'Hồ sơ bệnh án', icon: FileText, color: 'bg-purple-500' },
  { id: 'identifiers' as SectionType, name: 'Định danh y tế', icon: Fingerprint, color: 'bg-orange-500' },
  { id: 'health' as SectionType, name: 'Chỉ số sức khỏe', icon: Heart, color: 'bg-red-500' },
  { id: 'settings' as SectionType, name: 'Cài đặt', icon: Settings, color: 'bg-gray-500' },
];

export default function PatientPortal() {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [patient, setPatient] = useState<Patient | null>(null);
  const [activeSection, setActiveSection] = useState<SectionType>('profile');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetchUserData();
  }, []);

  const fetchUserData = async () => {
    setIsLoading(true);
    try {
      const userResponse = await authApi.getProfile();
      const userData = userResponse.data;
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

      if (userData.patientId) {
        const patientResponse = await api.get(`/patients/${userData.patientId}`);
        setPatient(patientResponse.data);
      }
    } catch (error) {
      console.error('Error fetching user data:', error);
      toast.error('Failed to load profile data');
    } finally {
      setIsLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    router.push('/');
    toast.success('Đăng xuất thành công!');
  };

  const renderSection = () => {
    switch (activeSection) {
      case 'profile':
        return <ProfileSection user={user} patient={patient} onRefresh={fetchUserData} />;
      case 'appointments':
        return <AppointmentsSection patientId={patient?.id || null} />;
      case 'records':
        return <MedicalRecordsSection patientId={patient?.id || null} />;
      case 'identifiers':
        return <IdentifiersSection patientId={patient?.id || null} />;
      case 'health':
        return <HealthMetricsSection patientId={patient?.id || null} />;
      case 'settings':
        return <SettingsSection user={user} />;
      default:
        return <ProfileSection user={user} patient={patient} onRefresh={fetchUserData} />;
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100">
      <div className="flex h-screen">
        {/* Sidebar */}
        <aside className="w-64 bg-white shadow-xl">
          {/* Header */}
          <div className="p-6 border-b border-gray-200">
            <h1 className="text-2xl font-bold text-blue-600">HMS Portal</h1>
            <p className="text-sm text-gray-500 mt-1">Patient Dashboard</p>
          </div>

          {/* User Info */}
          <div className="p-4 border-b border-gray-200">
            <div className="flex items-center space-x-3">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-blue-400 to-indigo-500 flex items-center justify-center text-white font-semibold text-lg shadow-md">
                {user?.firstName?.[0]}{user?.lastName?.[0]}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-gray-900 truncate">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="text-xs text-gray-500 truncate">{user?.email}</p>
              </div>
              <Activity className="w-5 h-5 text-green-500 animate-pulse" />
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
            {sidebarItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeSection === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => setActiveSection(item.id)}
                  className={`w-full flex items-center space-x-3 px-4 py-3 rounded-lg transition-all duration-200 group ${
                    isActive
                      ? 'bg-blue-50 text-blue-600 shadow-sm'
                      : 'text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  <div className={`p-2 rounded-lg transition-all duration-200 ${
                    isActive ? `${item.color} text-white shadow-md` : 'bg-gray-100 text-gray-400 group-hover:bg-gray-200'
                  }`}>
                    <Icon className={`w-5 h-5 ${isActive ? 'animate-pulse' : 'group-hover:scale-110 transition-transform'}`} />
                  </div>
                  <span className="font-medium text-sm">{item.name}</span>
                </button>
              );
            })}
          </nav>

          {/* Logout */}
          <div className="p-4 border-t border-gray-200">
            <button
              onClick={handleLogout}
              className="w-full flex items-center space-x-3 px-4 py-3 rounded-lg text-red-600 hover:bg-red-50 transition-all duration-200 group"
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
            {renderSection()}
          </div>
        </main>
      </div>
    </div>
  );
}