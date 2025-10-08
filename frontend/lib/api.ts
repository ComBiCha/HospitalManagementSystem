import axios from 'axios'

// API URL configuration
const API_URL = typeof window === 'undefined' 
  ? 'http://hms-api-service/api'  
  : '/api'  

// Debug logging
console.log('🔧 API Configuration:', {
  API_URL: API_URL,
  NODE_ENV: process.env.NODE_ENV
})

export const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000, // 10 seconds timeout
})



// Request interceptor to add auth token
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor to handle token refresh
let isRefreshing = false

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Nếu đã thử refresh rồi thì không thử lại nữa
    if (error.response?.status === 401 && !originalRequest._retry && !isRefreshing) {
      originalRequest._retry = true
      isRefreshing = true

      const refreshToken = localStorage.getItem('refreshToken')
      if (refreshToken) {
        try {
          const response = await api.post('/auth/refresh', { refreshToken })
          const { token, refreshToken: newRefreshToken } = response.data
          localStorage.setItem('token', token)
          localStorage.setItem('refreshToken', newRefreshToken)
          originalRequest.headers.Authorization = `Bearer ${token}`
          isRefreshing = false
          return api(originalRequest)
        } catch (refreshError) {
          localStorage.removeItem('token')
          localStorage.removeItem('refreshToken')
          isRefreshing = false
          window.location.href = '/'
        }
      } else {
        isRefreshing = false
        window.location.href = '/'
      }
    }

    // Nếu đã refresh rồi mà vẫn lỗi thì không redirect nữa
    return Promise.reject(error)
  }
)

export interface LoginRequest {
  username: string
  password: string
}

export interface RegisterRequest {
  username: string
  email: string
  password: string
  firstName: string
  lastName: string
  role: string
}

export interface AuthResponse {
  token: string
  refreshToken: string
  expiresAt: string
  user: UserInfo
}

export interface UserInfo {
  id: number
  username: string
  email: string
  firstName: string
  lastName: string
  role: string
  patientId?: number
  doctorId?: number
}

export const authApi = {
  login: (data: LoginRequest) => api.post<AuthResponse>('/auth/login', data),
  register: (data: RegisterRequest) => api.post<AuthResponse>('/auth/register', data),
  logout: (refreshToken: string) => api.post('/auth/logout', { refreshToken }),
  validateToken: (token: string) => api.post('/auth/validate', { token }),
  getProfile: () => api.get<UserInfo>('/auth/profile'),
}

// Doctor Attendance APIs
export const doctorAttendanceApi = {
  getCheckInStatus: () => api.get('/DoctorAttendance/check-in-status'),
  checkIn: (note?: string) => api.post('/DoctorAttendance/check-in', { note }),
  checkOut: (note?: string) => api.post('/DoctorAttendance/check-out', { note }),
  getTodayAttendance: () => api.get('/DoctorAttendance/today'),
  getMyAttendances: (startDate?: string, endDate?: string) => 
    api.get('/DoctorAttendance/my-attendances', { 
      params: { startDate, endDate } 
    }),
}

// Notification APIs
export const notificationApi = {
  getMyNotifications: (limit?: number) => 
    api.get('/Notifications', { params: { limit } }),
  getUnread: () => api.get('/Notifications/unread'),
  markAsRead: (id: number) => api.put(`/Notifications/${id}/mark-read`),
  markAllAsRead: () => api.put('/Notifications/mark-all-read'),
  deleteNotification: (id: number) => api.delete(`/Notifications/${id}`),
};

// Doctor APIs
export const doctorApi = {
  getById: (id: number) => api.get(`/Doctors/${id}`),
  getAll: () => api.get('/Doctors'),
  getMyProfile: () => api.get('/Doctors/my-profile'),
};

// Appointment APIs
export const appointmentApi = {
  getMyAppointments: (startDate?: string, endDate?: string) => 
    api.get('/Appointments/my-appointments', { 
      params: { startDate, endDate } 
    }),
  getById: (id: number) => api.get(`/Appointments/${id}`),
  updateStatus: (id: number, status: string) => 
    api.put(`/Appointments/${id}/status`, { status }),
};
