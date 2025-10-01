'use client';

import { User } from '@/lib/types';

interface SettingsSectionProps {
  user: User | null;
}

export default function SettingsSection({ user }: SettingsSectionProps) {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Cài đặt</h2>
        <p className="text-gray-600 mt-1">Quản lý cài đặt tài khoản và thông báo</p>
      </div>

      {/* Notification Settings */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Thông báo</h3>
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium text-gray-900">Email thông báo</p>
              <p className="text-sm text-gray-500">Nhận thông báo qua email</p>
            </div>
            <button className="relative inline-flex h-6 w-11 items-center rounded-full bg-blue-600">
              <span className="translate-x-6 inline-block h-4 w-4 transform rounded-full bg-white transition" />
            </button>
          </div>
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium text-gray-900">Nhắc lịch hẹn</p>
              <p className="text-sm text-gray-500">Nhận thông báo nhắc lịch hẹn</p>
            </div>
            <button className="relative inline-flex h-6 w-11 items-center rounded-full bg-blue-600">
              <span className="translate-x-6 inline-block h-4 w-4 transform rounded-full bg-white transition" />
            </button>
          </div>
        </div>
      </div>

      {/* Security Settings */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Bảo mật</h3>
        <button className="w-full sm:w-auto px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50">
          Đổi mật khẩu
        </button>
      </div>
    </div>
  );
}