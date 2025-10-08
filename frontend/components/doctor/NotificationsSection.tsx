'use client';

import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { notificationApi } from '@/lib/api';
import { Notification } from '@/lib/types';
import {
  Bell,
  Check,
  Trash2,
  CheckCheck,
  Clock,
  LogIn,
  LogOut,
  AlertCircle,
  Sparkles,
  X,
} from 'lucide-react';

interface NotificationsSectionProps {
  onCountChange?: (count: number) => void;
}

export default function NotificationsSection({ onCountChange }: NotificationsSectionProps) {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [filter, setFilter] = useState<'all' | 'unread'>('all');

  useEffect(() => {
    fetchNotifications();
    fetchUnreadCount();
    
    // Poll for new notifications every 30 seconds
    const interval = setInterval(() => {
      fetchUnreadCount();
    }, 30000);
    
    return () => clearInterval(interval);
  }, []);

  const fetchNotifications = async () => {
    setIsLoading(true);
    try {
      const response = await notificationApi.getMyNotifications(50);
      setNotifications(response.data);
    } catch (error: any) {
      console.error('Error fetching notifications:', error);
      toast.error('Không thể tải thông báo');
    } finally {
      setIsLoading(false);
    }
  };

  const fetchUnreadCount = async () => {
    try {
      const response = await notificationApi.getUnread();
      const count = response.data.count;
      setUnreadCount(count);
      onCountChange?.(count); // Notify parent component
    } catch (error) {
      console.error('Error fetching unread count:', error);
    }
  };

  const handleMarkAsRead = async (id: number) => {
    try {
      await notificationApi.markAsRead(id);
      setNotifications(notifications.map(n => 
        n.id === id ? { ...n, isRead: true } : n
      ));
      const newCount = Math.max(0, unreadCount - 1);
      setUnreadCount(newCount);
      onCountChange?.(newCount); // Update parent
      toast.success('Đã đánh dấu đã đọc');
    } catch (error) {
      toast.error('Lỗi khi cập nhật');
    }
  };

  const handleMarkAllAsRead = async () => {
    try {
      await notificationApi.markAllAsRead();
      setNotifications(notifications.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
      onCountChange?.(0); // Update parent
      toast.success('Đã đánh dấu tất cả đã đọc');
    } catch (error) {
      toast.error('Lỗi khi cập nhật');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await notificationApi.deleteNotification(id);
      setNotifications(notifications.filter(n => n.id !== id));
      toast.success('Đã xóa thông báo');
    } catch (error) {
      toast.error('Lỗi khi xóa');
    }
  };

  const getNotificationIcon = (metadata?: string) => {
    const type = metadata ? JSON.parse(metadata).type : 'default';
    switch (type) {
      case 'CheckIn':
        return <LogIn className="w-5 h-5 text-emerald-600" />;
      case 'CheckOut':
        return <LogOut className="w-5 h-5 text-blue-600" />;
      case 'Reminder':
        return <Clock className="w-5 h-5 text-amber-600" />;
      case 'Alert':
        return <AlertCircle className="w-5 h-5 text-red-600" />;
      default:
        return <Bell className="w-5 h-5 text-gray-600" />;
    }
  };

  const getNotificationColor = (metadata?: string) => {
    const type = metadata ? JSON.parse(metadata).type : 'default';
    switch (type) {
      case 'CheckIn':
        return 'bg-emerald-50 border-emerald-200';
      case 'CheckOut':
        return 'bg-blue-50 border-blue-200';
      case 'Reminder':
        return 'bg-amber-50 border-amber-200';
      case 'Alert':
        return 'bg-red-50 border-red-200';
      default:
        return 'bg-gray-50 border-gray-200';
    }
  };

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (minutes < 1) return 'Vừa xong';
    if (minutes < 60) return `${minutes} phút trước`;
    if (hours < 24) return `${hours} giờ trước`;
    if (days < 7) return `${days} ngày trước`;
    return date.toLocaleDateString('vi-VN');
  };

  const filteredNotifications = filter === 'unread' 
    ? notifications.filter(n => !n.isRead)
    : notifications;

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
      {/* Header */}
      <div className="bg-white rounded-2xl shadow-xl p-6 border border-emerald-100">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center space-x-3">
            <div className="p-3 bg-amber-100 rounded-xl">
              <Bell className="w-6 h-6 text-amber-600" />
            </div>
            <div>
              <h3 className="text-xl font-bold text-gray-900">Thông báo</h3>
              <p className="text-sm text-gray-500">
                {unreadCount > 0 ? `${unreadCount} thông báo chưa đọc` : 'Tất cả đã đọc'}
              </p>
            </div>
          </div>
          
          {unreadCount > 0 && (
            <button
              onClick={handleMarkAllAsRead}
              className="flex items-center space-x-2 px-4 py-2 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-all"
            >
              <CheckCheck className="w-4 h-4" />
              <span className="text-sm font-medium">Đánh dấu tất cả</span>
            </button>
          )}
        </div>

        {/* Filter */}
        <div className="flex space-x-2">
          <button
            onClick={() => setFilter('all')}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${
              filter === 'all'
                ? 'bg-emerald-500 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            Tất cả ({notifications.length})
          </button>
          <button
            onClick={() => setFilter('unread')}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${
              filter === 'unread'
                ? 'bg-emerald-500 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            Chưa đọc ({unreadCount})
          </button>
        </div>
      </div>

      {/* Notifications List */}
      <div className="space-y-3">
        {filteredNotifications.length === 0 ? (
          <div className="bg-white rounded-2xl shadow-xl p-12 border border-gray-200 text-center">
            <div className="p-4 bg-gray-100 rounded-full w-20 h-20 mx-auto mb-4 flex items-center justify-center">
              <Bell className="w-10 h-10 text-gray-400" />
            </div>
            <h3 className="text-xl font-bold text-gray-900 mb-2">
              {filter === 'unread' ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo'}
            </h3>
            <p className="text-gray-500">
              {filter === 'unread' 
                ? 'Tất cả thông báo đã được đọc' 
                : 'Bạn sẽ nhận được thông báo ở đây'}
            </p>
          </div>
        ) : (
          filteredNotifications.map((notification) => {
            const metadata = notification.metadata ? JSON.parse(notification.metadata) : {};
            const notificationType = metadata.type || 'default';
            
            return (
            <div
              key={notification.id}
              className={`bg-white rounded-xl shadow-lg p-5 border-2 transition-all hover:shadow-xl ${
                getNotificationColor(notification.metadata)
              } ${!notification.isRead ? 'ring-2 ring-emerald-200' : ''}`}
            >
              <div className="flex items-start space-x-4">
                <div className={`p-3 rounded-xl ${
                  notificationType === 'CheckIn' ? 'bg-emerald-100' :
                  notificationType === 'CheckOut' ? 'bg-blue-100' :
                  notificationType === 'Reminder' ? 'bg-amber-100' :
                  notificationType === 'Alert' ? 'bg-red-100' : 'bg-gray-100'
                }`}>
                  {getNotificationIcon(notification.metadata)}
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-start justify-between mb-2">
                    <h4 className="text-base font-bold text-gray-900 flex items-center space-x-2">
                      <span>{notification.subject}</span>
                      {!notification.isRead && (
                        <span className="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
                      )}
                    </h4>
                    <span className="text-xs text-gray-500 whitespace-nowrap ml-2">
                      {formatTime(notification.createdAt)}
                    </span>
                  </div>
                  
                  <p className="text-sm text-gray-700 mb-3">{notification.content}</p>

                  <div className="flex items-center space-x-2">
                    {!notification.isRead && (
                      <button
                        onClick={() => handleMarkAsRead(notification.id)}
                        className="flex items-center space-x-1 px-3 py-1.5 bg-emerald-100 text-emerald-700 rounded-lg hover:bg-emerald-200 transition-all text-xs font-medium"
                      >
                        <Check className="w-3 h-3" />
                        <span>Đánh dấu đã đọc</span>
                      </button>
                    )}
                    
                    <button
                      onClick={() => handleDelete(notification.id)}
                      className="flex items-center space-x-1 px-3 py-1.5 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition-all text-xs font-medium"
                    >
                      <Trash2 className="w-3 h-3" />
                      <span>Xóa</span>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )})
        )}
      </div>

      {/* Empty state animation */}
      {filteredNotifications.length === 0 && (
        <div className="flex justify-center">
          <Sparkles className="w-6 h-6 text-emerald-400 animate-pulse" />
        </div>
      )}
    </div>
  );
}
