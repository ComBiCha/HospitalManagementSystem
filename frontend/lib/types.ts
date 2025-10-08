// User types
export interface User {
  id: number
  username: string
  email: string
  firstName: string
  lastName: string
  role: string
  isActive: boolean
  createdAt: string
  updatedAt: string
  lastLoginAt?: string
  patientId?: number
  doctorId?: number
}

// UserInfo type (from API response) - matches api.ts
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

// Patient types
export interface Patient {
  id: number
  name: string
  age: number
  email: string
  status: string
  createdAt: string
  updatedAt?: string
}

// Patient Identifier types
export interface PatientIdentifier {
  id: number
  patientId: number
  ehrSystem: string
  externalId: string
  identifierType: string
  isActive: boolean
  createdAt: string
  updatedAt?: string
}

// Appointment types
export interface Appointment {
  id: number
  patientId: number
  doctorId: number
  date: string
  status: string
  createdAt: string
  updatedAt: string
  doctor?: Doctor
  patient?: Patient
}

// Doctor types
export interface Doctor {
  id: number
  name: string
  specialty: string
  email: string
  status: number
  createdAt: string
  updatedAt: string
  isActive: boolean
  isOnDuty: boolean
  isOffDuty: boolean
  isOnLeave: boolean
  isOnCall: boolean
  isInSurgery: boolean
  isOnVacation: boolean
}

// Auth types
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
  user: User
}

// API Response types
export interface ApiResponse<T> {
  data: T
  message?: string
  success: boolean
}

// Form types
export interface UserFormData {
  firstName: string
  lastName: string
  email: string
  username: string
}

export interface PatientFormData {
  name: string
  age: number
  email: string
}

export interface PatientIdentifierFormData {
  ehrSystem: number
  externalId: string
  identifierType: string
}

// DoctorShift types
export interface DoctorShift {
  id: number;
  doctorId: number;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

// DoctorAttendance types
export interface DoctorAttendance {
  id: number;
  doctorId: number;
  shiftId: number;
  shiftDate: string;
  checkInTime?: string;
  checkOutTime?: string;
  status: 'Scheduled' | 'CheckedIn' | 'CheckedOut' | 'Absent';
  checkInNote?: string;
  checkOutNote?: string;
  shift?: DoctorShift;
  createdAt: string;
  updatedAt?: string;
}

// CheckInStatus types
export interface CheckInStatus {
  canCheckIn: boolean;
  isCheckedIn: boolean;
  canCheckOut?: boolean;
  message: string;
  shift?: DoctorShift;
  allShifts?: DoctorShift[];
  attendance?: DoctorAttendance;
  earliestCheckInTime?: string;
  shiftStartTime?: string;
  isCompleted?: boolean;
}

// Notification types
export interface Notification {
  id: number;
  userId: number;
  recipient: string;
  subject: string; // Title
  content: string; // Message
  channelType: string;
  status: string;
  isRead: boolean;
  metadata?: string; // Contains type and data
  createdAt: string;
  sentAt?: string;
}
