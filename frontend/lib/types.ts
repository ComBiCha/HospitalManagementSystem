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
}

// Doctor types
export interface Doctor {
  id: number
  name: string
  specialty: string
  email: string
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
