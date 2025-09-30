'use client';

import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import Header from '@/components/Header'
import ProfileSection from '@/components/profile/ProfileSection'
import PatientSection from '@/components/profile/PatientSection'
import IdentifiersSection from '@/components/profile/IdentifiersSection'
import AppointmentHistory from '@/components/profile/AppointmentHistory'
import { 
  User, 
  Patient, 
  PatientIdentifier, 
  UserFormData, 
  PatientFormData, 
  PatientIdentifierFormData 
} from '@/lib/types'
import { api, authApi } from '@/lib/api'
import toast from 'react-hot-toast'

export default function ProfilePage() {
  const router = useRouter()
  const [user, setUser] = useState<User | null>(null)
  const [patient, setPatient] = useState<Patient | null>(null)
  const [identifiers, setIdentifiers] = useState<PatientIdentifier[]>([])
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) {
      router.push('/')
      return
    }
    
    fetchUserData()
  }, [router])

  const fetchUserData = async () => {
    setIsLoading(true)
    try {
      // Fetch user profile using authApi
      const userResponse = await authApi.getProfile()
      const userData = userResponse.data
      setUser({
        id: userData.id,
        username: userData.username,
        email: userData.email,
        firstName: userData.firstName,
        lastName: userData.lastName,
        role: userData.role,
        isActive: true, // API doesn't return this, assume true
        createdAt: new Date().toISOString(), // API doesn't return this
        updatedAt: new Date().toISOString(), // API doesn't return this
        patientId: userData.patientId,
        doctorId: userData.doctorId
      })
      
      // If user has a patient ID, fetch patient data
      if (userData.patientId) {
        await fetchPatientData(userData.patientId)
      }
    } catch (error) {
      console.error('Error fetching user data:', error)
      toast.error('Failed to load profile data')
      router.push('/')
    } finally {
      setIsLoading(false)
    }
  }

  const fetchPatientData = async (patientId: number) => {
    try {
      // Fetch patient info using api
      const patientResponse = await api.get(`/patients/${patientId}`)
      setPatient(patientResponse.data)
      
      // Fetch patient identifiers
      const identifiersResponse = await api.get(`/patients/${patientId}/identifiers`)
      setIdentifiers(identifiersResponse.data || [])
    } catch (error) {
      console.error('Error fetching patient data:', error)
    }
  }

  const handleUserUpdate = async (formData: UserFormData) => {
    try {
      // Đảm bảo truyền đủ các trường required khi update user
      const fullData = {
        email: formData.email ?? user?.email ?? '',
        firstName: formData.firstName ?? user?.firstName ?? '',
        lastName: formData.lastName ?? user?.lastName ?? '',
        role: user?.role ?? 'Patient',
        patientId: user?.patientId ?? null,
        doctorId: user?.doctorId ?? null
      }
      const response = await api.put(`/users/${user?.id}`, fullData)
      setUser((prev: User | null) =>
        prev
          ? {
              ...prev,
              ...fullData,
              patientId: fullData.patientId === null ? undefined : fullData.patientId,
              doctorId: fullData.doctorId === null ? undefined : fullData.doctorId,
            }
          : null
      )
      toast.success('Profile updated successfully!')
    } catch (error) {
      console.error('Error updating profile:', error)
      toast.error('Failed to update profile')
      throw error
    }
  }

  const handlePatientCreate = async (formData: PatientFormData) => {
    try {
      const response = await api.post('/patients', formData)
      setPatient(response.data)
      // Update user.patientId in DB
      if (user) {
        await api.put(`/users/${user.id}`, { patientId: response.data.id })
        setUser((prev: User | null) => prev ? { ...prev, patientId: response.data.id } : null)
      }
      toast.success('Patient profile created successfully!')
    } catch (error) {
      console.error('Error creating patient:', error)
      toast.error('Failed to create patient profile')
      throw error
    }
  }

  const handlePatientUpdate = async (formData: PatientFormData) => {
    if (!patient) return
    try {
      // Đảm bảo truyền đủ trường id khi update patient
      const fullData = { id: patient.id, ...formData }
      const response = await api.put(`/patients/${patient.id}`, fullData)
      setPatient(response.data)
      toast.success('Patient information updated successfully!')
    } catch (error) {
      console.error('Error updating patient:', error)
      toast.error('Failed to update patient information')
      throw error
    }
  }

  const handleIdentifierAdd = async (formData: PatientIdentifierFormData) => {
    if (!patient) return
    
    try {
      const response = await api.post(`/patients/${patient.id}/identifiers`, formData)
      setIdentifiers([...identifiers, response.data])
      toast.success('Identifier added successfully!')
    } catch (error) {
      console.error('Error adding identifier:', error)
      toast.error('Failed to add identifier')
      throw error
    }
  }

  const handleIdentifierUpdate = async (id: number, formData: PatientIdentifierFormData) => {
    try {
      const response = await api.put(`/patients/identifiers/${id}`, formData)
      setIdentifiers(identifiers.map(item => 
        item.id === id ? response.data : item
      ))
      toast.success('Identifier updated successfully!')
    } catch (error) {
      console.error('Error updating identifier:', error)
      toast.error('Failed to update identifier')
      throw error
    }
  }

  const handleIdentifierDelete = async (id: number) => {
    try {
      await api.delete(`/patients/identifiers/${id}`)
      setIdentifiers(identifiers.filter(item => item.id !== id))
      toast.success('Identifier deleted successfully!')
    } catch (error) {
      console.error('Error deleting identifier:', error)
      toast.error('Failed to delete identifier')
      throw error
    }
  }

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-50">
        <Header onAuthClick={() => router.push('/')} />
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          <div className="flex justify-center items-center h-64">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
          </div>
        </div>
      </div>
    )
  }

  if (!user) {
    return (
      <div className="min-h-screen bg-gray-50">
        <Header onAuthClick={() => router.push('/')} />
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          <div className="text-center">
            <h1 className="text-2xl font-bold text-gray-900">Profile not found</h1>
            <p className="text-gray-600">Please log in to view your profile.</p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <Header onAuthClick={() => router.push('/')} />
      
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">Profile</h1>
          <p className="text-gray-600">Manage your account and patient information</p>
        </div>

        <div className="space-y-8">
          {/* User Profile Section */}
          <ProfileSection 
            user={user} 
            onUpdate={handleUserUpdate} 
          />

          {/* Patient Information Section */}
          <PatientSection 
            patient={patient}
            onUpdate={handlePatientUpdate}
            onCreate={handlePatientCreate}
          />

          {/* Patient Identifiers Section */}
          {patient && (
            <IdentifiersSection 
              identifiers={identifiers}
              onAdd={handleIdentifierAdd}
              onUpdate={handleIdentifierUpdate}
              onDelete={handleIdentifierDelete}
            />
          )}

          {/* Appointment History Section */}
          <AppointmentHistory patientId={patient?.id || null} />
        </div>
      </div>
    </div>
  )
}
