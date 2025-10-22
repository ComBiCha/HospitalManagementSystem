'use client';

import { useState, useEffect, Fragment } from 'react';
import { User, Patient, UserFormData } from '@/lib/types';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { Dialog, Transition } from '@headlessui/react';
import { UploadCloud, X, CheckCircle, AlertTriangle } from 'lucide-react';

// DTO for the preview data from the backend
interface PatientImportPreviewDto {
  epicPatientId: string;
  firstName: string;
  lastName: string;
  email: string;
  dateOfBirth: string; // Comes as string from JSON
}

// DTO for the patient form data
interface PatientFormData {
  name: string;
  dateOfBirth: string; // Use string to match input type
  email: string;
}

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

  // New state for the import modal
  const [showImportModal, setShowImportModal] = useState(false);
  const [epicId, setEpicId] = useState('');
  const [importPreviewData, setImportPreviewData] = useState<PatientImportPreviewDto | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);

  const [userFormData, setUserFormData] = useState<UserFormData>({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    email: user?.email || '',
    username: user?.username || '',
  });

  const [patientFormData, setPatientFormData] = useState<PatientFormData>({
    name: patient?.name || '',
    dateOfBirth: patient?.dateOfBirth ? new Date(patient.dateOfBirth).toISOString().split('T')[0] : '',
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
        dateOfBirth: patient.dateOfBirth ? new Date(patient.dateOfBirth).toISOString().split('T')[0] : '',
        email: patient.email,
      });
      setIsCreatingPatient(false);
    } else {
      setIsCreatingPatient(true);
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

  const refreshSession = async () => {
    const refreshToken = localStorage.getItem('refreshToken');
    if (refreshToken) {
      try {
        const refreshResponse = await api.post('/auth/refresh', { refreshToken });
        localStorage.setItem('token', refreshResponse.data.token);
        localStorage.setItem('refreshToken', refreshResponse.data.refreshToken);
        toast.success('Phiên làm việc đã được làm mới!');
        onRefresh();
      } catch (error) {
        toast.error('Vui lòng đăng nhập lại để cập nhật thông tin.');
        localStorage.removeItem('token');
        localStorage.removeItem('refreshToken');
        window.location.href = '/';
      }
    } else {
        toast.error('Không tìm thấy phiên đăng nhập, vui lòng đăng nhập lại.');
        window.location.href = '/';
    }
  }

  const handleCreatePatient = async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const dto = {
        Name: patientFormData.name,
        DateOfBirth: patientFormData.dateOfBirth,
        Email: patientFormData.email,
        Identifiers: []
      };
      const response = await api.post('/patients/full', dto);
      await api.put(`/users/${user.id}`, { patientId: response.data.id });
      toast.success('Tạo hồ sơ bệnh nhân thành công!');
      await refreshSession();
      setIsCreatingPatient(false);
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

  const handleVerifyEpicId = async () => {
    if (!epicId) {
      setImportError('Vui lòng nhập Epic Patient ID.');
      return;
    }
    setIsVerifying(true);
    setImportError(null);
    setImportPreviewData(null);
    try {
      const response = await api.get(`/patients/import-preview?epicId=${epicId}`);
      setImportPreviewData(response.data);
    } catch (error: any) {
      setImportError(error.response?.data?.message || 'Không tìm thấy bệnh nhân với ID này.');
    } finally {
      setIsVerifying(false);
    }
  };

  const handleConfirmImport = async () => {
    if (!importPreviewData) return;
    setIsConfirming(true);
    setImportError(null);
    try {
      await api.post('/patients/import-confirm', importPreviewData);
      toast.success('Nhập và tạo hồ sơ thành công!');
      closeImportModal();
      await refreshSession();
    } catch (error: any) {
      setImportError(error.response?.data?.message || 'Lỗi khi xác nhận nhập.');
    } finally {
      setIsConfirming(false);
    }
  };

  const closeImportModal = () => {
    setShowImportModal(false);
    setEpicId('');
    setImportPreviewData(null);
    setImportError(null);
  }

  return (
    <>
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
                <label className="block text-sm font-medium text-gray-700 mb-1">Ngày sinh</label>
                {isEditingPatient ? (
                  <input
                    type="date"
                    value={patientFormData.dateOfBirth}
                    onChange={(e) => setPatientFormData({ ...patientFormData, dateOfBirth: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                ) : (
                  <p className="text-gray-900">{patient.dateOfBirth ? new Date(patient.dateOfBirth).toLocaleDateString('vi-VN') : '-'}</p>
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
              <button 
                onClick={() => setShowImportModal(true)}
                className="flex items-center space-x-2 text-sm font-medium text-blue-600 hover:text-blue-800"
              >
                <UploadCloud className="w-4 h-4" />
                <span>Hoặc nhập từ Epic</span>
              </button>
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
                <label className="block text-sm font-medium text-gray-700 mb-1">Ngày sinh</label>
                <input
                  type="date"
                  value={patientFormData.dateOfBirth}
                  onChange={(e) => setPatientFormData({ ...patientFormData, dateOfBirth: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
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
            <div className="mt-6 flex justify-end">
                <button
                    onClick={handleCreatePatient}
                    disabled={isLoading}
                    className="bg-blue-600 text-white px-6 py-2 rounded-md hover:bg-blue-700 text-sm font-semibold disabled:opacity-50"
                >
                    {isLoading ? 'Đang tạo...' : 'Tạo hồ sơ'}
                </button>
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
            <div className="flex justify-center items-center space-x-4">
              <button
                onClick={() => setIsCreatingPatient(true)}
                className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
              >
                Tạo hồ sơ bệnh nhân
              </button>
              <button 
                onClick={() => setShowImportModal(true)}
                className="inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50"
              >
                <UploadCloud className="w-4 h-4 mr-2" />
                Nhập từ Epic
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Import Modal */}
      <Transition appear show={showImportModal} as={Fragment}>
        <Dialog as="div" className="relative z-10" onClose={closeImportModal}>
          <Transition.Child
            as={Fragment}
            enter="ease-out duration-300"
            enterFrom="opacity-0"
            enterTo="opacity-100"
            leave="ease-in duration-200"
            leaveFrom="opacity-100"
            leaveTo="opacity-0"
          >
            <div className="fixed inset-0 bg-black bg-opacity-25" />
          </Transition.Child>

          <div className="fixed inset-0 overflow-y-auto">
            <div className="flex min-h-full items-center justify-center p-4 text-center">
              <Transition.Child
                as={Fragment}
                enter="ease-out duration-300"
                enterFrom="opacity-0 scale-95"
                enterTo="opacity-100 scale-100"
                leave="ease-in duration-200"
                leaveFrom="opacity-100 scale-100"
                leaveTo="opacity-0 scale-95"
              >
                <Dialog.Panel className="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                  <Dialog.Title as="h3" className="text-lg font-medium leading-6 text-gray-900 flex items-center">
                    <UploadCloud className="w-6 h-6 mr-2 text-blue-600" />
                    Nhập hồ sơ từ Epic
                  </Dialog.Title>
                  <button onClick={closeImportModal} className="absolute top-4 right-4 text-gray-400 hover:text-gray-600">
                    <X className="w-6 h-6" />
                  </button>

                  <div className="mt-4">
                    {!importPreviewData ? (
                      <div>
                        <p className="text-sm text-gray-500 mb-2">Nhập Epic Patient ID của bạn để tìm và nhập thông tin.</p>
                        <div className="flex items-center space-x-2">
                          <input 
                            type="text"
                            value={epicId}
                            onChange={(e) => setEpicId(e.target.value)}
                            placeholder="Epic Patient ID"
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                          />
                          <button 
                            onClick={handleVerifyEpicId}
                            disabled={isVerifying}
                            className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:bg-gray-400"
                          >
                            {isVerifying ? 'Đang tìm...' : 'Tìm'}
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div>
                        <p className="text-sm text-gray-500 mb-4">Vui lòng xác nhận thông tin dưới đây trước khi nhập.</p>
                        <div className="space-y-3 rounded-lg bg-gray-50 p-4 border border-gray-200">
                          <div className="flex justify-between">
                            <span className="font-medium text-gray-600">Họ và tên:</span>
                            <span className="font-semibold text-gray-900">{importPreviewData.firstName} {importPreviewData.lastName}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="font-medium text-gray-600">Email:</span>
                            <span className="font-semibold text-gray-900">{importPreviewData.email}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="font-medium text-gray-600">Ngày sinh:</span>
                            <span className="font-semibold text-gray-900">{new Date(importPreviewData.dateOfBirth).toLocaleDateString('vi-VN')}</span>
                          </div>
                        </div>
                        <div className="mt-6 flex justify-end space-x-3">
                          <button onClick={() => setImportPreviewData(null)} className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200">Quay lại</button>
                          <button 
                            onClick={handleConfirmImport}
                            disabled={isConfirming}
                            className="px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 disabled:bg-gray-400 flex items-center"
                          >
                            <CheckCircle className="w-4 h-4 mr-2" />
                            {isConfirming ? 'Đang nhập...' : 'Xác nhận và tạo hồ sơ'}
                          </button>
                        </div>
                      </div>
                    )}

                    {importError && (
                      <div className="mt-4 flex items-center space-x-2 text-sm text-red-600 bg-red-50 p-3 rounded-lg">
                        <AlertTriangle className="w-5 h-5" />
                        <span>{importError}</span>
                      </div>
                    )}
                  </div>
                </Dialog.Panel>
              </Transition.Child>
            </div>
          </div>
        </Dialog>
      </Transition>
    </>
  );
}