'use client';

import { useState, useEffect } from 'react';
import { User, Patient, UserFormData, PatientFormData } from '@/lib/types';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';

interface ProfileSectionProps {
  user: User | null;
  patient: Patient | null;
  onRefresh: () => void;
}

export default function ProfileSection({ user, patient, onRefresh }: ProfileSectionProps) {
  const [isEditingUser, setIsEditingUser] = useState(false);
  const [isEditingPatient, setIsEditingPatient] = useState(false);
  const [isCreatingPatient, setIsCreatingPatient] = useState(!patient);
  const [isLoading, setIsLoading] = useState(false);

  const [userFormData, setUserFormData] = useState<UserFormData>({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    email: user?.email || '',
    username: user?.username || '',
  });

  const [patientFormData, setPatientFormData] = useState<PatientFormData>({
    name: patient?.name || '',
    age: patient?.age || 0,
    email: patient?.email || '',
  });

  // Update form data when user changes
  useEffect(() => {
    if (user) {
      setUserFormData({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        username: user.username,
      });
    }
  }, [user]);

  // Update form data when patient changes
  useEffect(() => {
    if (patient) {
      setPatientFormData({
        name: patient.name,
        age: patient.age,
        email: patient.email,
      });
      setIsCreatingPatient(false);
    }
  }, [patient]);

  const handleUpdateUser = async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const fullData = {
        id: user.id,
        username: userFormData.username,
        email: userFormData.email,
        firstName: userFormData.firstName,
        lastName: userFormData.lastName,
        role: user.role,
        patientId: user.patientId ?? null,
        doctorId: user.doctorId ?? null,
        isActive: user.isActive,
      };
      await api.put(`/users/${user.id}`, fullData);
      toast.success('Cập nhật thông tin tài khoản thành công!');
      setIsEditingUser(false);
      onRefresh();
    } catch (error) {
      console.error('Error updating user:', error);
      toast.error('Cập nhật thông tin tài khoản thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreatePatient = async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const response = await api.post('/patients', patientFormData);
      await api.put(`/users/${user.id}`, { patientId: response.data.id });
      toast.success('Tạo hồ sơ bệnh nhân thành công!');
      setIsCreatingPatient(false);
      onRefresh();
    } catch (error) {
      console.error('Error creating patient:', error);
      toast.error('Tạo hồ sơ bệnh nhân thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  const handleUpdatePatient = async () => {
    if (!patient) return;
    setIsLoading(true);
    try {
      const fullData = { id: patient.id, ...patientFormData };
      await api.put(`/patients/${patient.id}`, fullData);
      toast.success('Cập nhật thông tin bệnh nhân thành công!');
      setIsEditingPatient(false);
      onRefresh();
    } catch (error) {
      console.error('Error updating patient:', error);
      toast.error('Cập nhật thông tin bệnh nhân thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Thông tin cá nhân</h2>
        <p className="text-gray-600 mt-1">Quản lý thông tin cá nhân và hồ sơ bệnh nhân của bạn</p>
      </div>

      {/* User Info Card */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-semibold text-gray-900">Thông tin tài khoản</h3>
          {!isEditingUser ? (
            <button
              onClick={() => setIsEditingUser(true)}
              className="text-blue-600 hover:text-blue-800 font-medium text-sm"
            >
              Chỉnh sửa
            </button>
          ) : (
            <div className="space-x-3">
              <button
                onClick={() => setIsEditingUser(false)}
                className="text-gray-600 hover:text-gray-800 font-medium text-sm"
                disabled={isLoading}
              >
                Hủy
              </button>
              <button
                onClick={handleUpdateUser}
                disabled={isLoading}
                className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 text-sm disabled:opacity-50"
              >
                {isLoading ? 'Đang lưu...' : 'Lưu'}
              </button>
            </div>
          )}
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Tên đăng nhập</label>
            {isEditingUser ? (
              <input
                type="text"
                value={userFormData.username}
                onChange={(e) => setUserFormData({ ...userFormData, username: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            ) : (
              <p className="text-gray-900">{user?.username || '-'}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            {isEditingUser ? (
              <input
                type="email"
                value={userFormData.email}
                onChange={(e) => setUserFormData({ ...userFormData, email: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            ) : (
              <p className="text-gray-900">{user?.email || '-'}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Họ</label>
            {isEditingUser ? (
              <input
                type="text"
                value={userFormData.firstName}
                onChange={(e) => setUserFormData({ ...userFormData, firstName: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            ) : (
              <p className="text-gray-900">{user?.firstName || '-'}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Tên</label>
            {isEditingUser ? (
              <input
                type="text"
                value={userFormData.lastName}
                onChange={(e) => setUserFormData({ ...userFormData, lastName: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            ) : (
              <p className="text-gray-900">{user?.lastName || '-'}</p>
            )}
          </div>
        </div>
      </div>

      {/* Patient Info Card */}
      {patient ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-lg font-semibold text-gray-900">Thông tin bệnh nhân</h3>
            {!isEditingPatient ? (
              <button
                onClick={() => setIsEditingPatient(true)}
                className="text-blue-600 hover:text-blue-800 font-medium text-sm"
              >
                Chỉnh sửa
              </button>
            ) : (
              <div className="space-x-3">
                <button
                  onClick={() => setIsEditingPatient(false)}
                  className="text-gray-600 hover:text-gray-800 font-medium text-sm"
                  disabled={isLoading}
                >
                  Hủy
                </button>
                <button
                  onClick={handleUpdatePatient}
                  disabled={isLoading}
                  className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 text-sm disabled:opacity-50"
                >
                  {isLoading ? 'Đang lưu...' : 'Lưu'}
                </button>
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Họ tên</label>
              {isEditingPatient ? (
                <input
                  type="text"
                  value={patientFormData.name}
                  onChange={(e) => setPatientFormData({ ...patientFormData, name: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              ) : (
                <p className="text-gray-900">{patient.name}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Tuổi</label>
              {isEditingPatient ? (
                <input
                  type="number"
                  value={patientFormData.age}
                  onChange={(e) => setPatientFormData({ ...patientFormData, age: parseInt(e.target.value) || 0 })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                  min="0"
                  max="150"
                />
              ) : (
                <p className="text-gray-900">{patient.age} tuổi</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
              {isEditingPatient ? (
                <input
                  type="email"
                  value={patientFormData.email}
                  onChange={(e) => setPatientFormData({ ...patientFormData, email: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              ) : (
                <p className="text-gray-900">{patient.email}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Trạng thái</label>
              <span className="inline-flex px-3 py-1 text-sm font-semibold rounded-full bg-green-100 text-green-800">
                {patient.status}
              </span>
            </div>
          </div>
        </div>
      ) : isCreatingPatient ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-lg font-semibold text-gray-900">Tạo hồ sơ bệnh nhân</h3>
            <div className="space-x-3">
              <button
                onClick={() => setIsCreatingPatient(false)}
                className="text-gray-600 hover:text-gray-800 font-medium text-sm"
                disabled={isLoading}
              >
                Hủy
              </button>
              <button
                onClick={handleCreatePatient}
                disabled={isLoading}
                className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 text-sm disabled:opacity-50"
              >
                {isLoading ? 'Đang tạo...' : 'Tạo'}
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Họ tên</label>
              <input
                type="text"
                value={patientFormData.name}
                onChange={(e) => setPatientFormData({ ...patientFormData, name: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Nhập họ tên"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Tuổi</label>
              <input
                type="number"
                value={patientFormData.age}
                onChange={(e) => setPatientFormData({ ...patientFormData, age: parseInt(e.target.value) || 0 })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Nhập tuổi"
                min="0"
                max="150"
              />
            </div>
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
              <input
                type="email"
                value={patientFormData.email}
                onChange={(e) => setPatientFormData({ ...patientFormData, email: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Nhập email"
              />
            </div>
          </div>
        </div>
      ) : (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ bệnh nhân</h3>
          <p className="text-gray-500 mb-4">Tạo hồ sơ bệnh nhân để sử dụng đầy đủ các tính năng.</p>
          <button
            onClick={() => setIsCreatingPatient(true)}
            className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
          >
            Tạo hồ sơ bệnh nhân
          </button>
        </div>
      )}
    </div>
  );
}