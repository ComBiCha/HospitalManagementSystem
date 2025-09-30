'use client';

import { useState, useEffect } from 'react'
import { Appointment } from '@/lib/types'
import { api } from '@/lib/api'

interface AppointmentHistoryProps {
  patientId: number | null
}

export default function AppointmentHistory({ patientId }: AppointmentHistoryProps) {
  const [appointments, setAppointments] = useState<Appointment[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (patientId) {
      fetchAppointments(patientId)
    }
  }, [patientId])

  const fetchAppointments = async (id: number) => {
    setIsLoading(true)
    setError(null)
    try {
      const response = await api.get(`/appointments/patient/${id}`)
      setAppointments(response.data || [])
    } catch (error) {
      console.error('Error fetching appointments:', error)
      setError('Error loading appointments')
    } finally {
      setIsLoading(false)
    }
  }

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'scheduled':
        return 'bg-blue-100 text-blue-800'
      case 'completed':
        return 'bg-green-100 text-green-800'
      case 'cancelled':
        return 'bg-red-100 text-red-800'
      case 'no-show':
        return 'bg-yellow-100 text-yellow-800'
      default:
        return 'bg-gray-100 text-gray-800'
    }
  }

  if (!patientId) {
    return (
      <div className="bg-white rounded-lg border shadow-sm p-6">
        <h2 className="text-xl font-semibold text-gray-900 mb-6">Appointment History</h2>
        <div className="text-center py-8">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3a2 2 0 012-2h4a2 2 0 012 2v4m-6 4v10m-6 0h12" />
            </svg>
          </div>
          <h3 className="text-sm font-medium text-gray-900 mb-1">No patient profile</h3>
          <p className="text-sm text-gray-500">Create a patient profile to view appointment history.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="bg-white rounded-lg border shadow-sm p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Appointment History</h2>
        <button
          onClick={() => fetchAppointments(patientId)}
          disabled={isLoading}
          className="text-blue-600 hover:text-blue-800 font-medium disabled:opacity-50"
        >
          {isLoading ? 'Refreshing...' : 'Refresh'}
        </button>
      </div>

      {isLoading ? (
        <div className="flex justify-center py-8">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      ) : error ? (
        <div className="text-center py-8">
          <div className="text-red-400 mb-4">
            <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h3 className="text-sm font-medium text-gray-900 mb-1">Error loading appointments</h3>
          <p className="text-sm text-gray-500">{error}</p>
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-8">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3a2 2 0 012-2h4a2 2 0 012 2v4m-6 4v10m-6 0h12" />
            </svg>
          </div>
          <h3 className="text-sm font-medium text-gray-900 mb-1">No appointments</h3>
          <p className="text-sm text-gray-500">No appointment history found for this patient.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {appointments
            .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
            .map((appointment) => (
              <div key={appointment.id} className="border border-gray-200 rounded-md p-4 hover:bg-gray-50">
                <div className="flex justify-between items-start">
                  <div className="flex-1">
                    <div className="flex items-center space-x-3 mb-2">
                      <h3 className="text-lg font-medium text-gray-900">
                        Appointment #{appointment.id}
                      </h3>
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${getStatusColor(appointment.status)}`}>
                        {appointment.status}
                      </span>
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                      <div>
                        <span className="font-medium text-gray-700">Date:</span>
                        <p className="text-gray-900">
                          {new Date(appointment.date).toLocaleDateString('en-US', {
                            weekday: 'long',
                            year: 'numeric',
                            month: 'long',
                            day: 'numeric'
                          })}
                        </p>
                      </div>
                      
                      <div>
                        <span className="font-medium text-gray-700">Time:</span>
                        <p className="text-gray-900">
                          {new Date(appointment.date).toLocaleTimeString('en-US', {
                            hour: '2-digit',
                            minute: '2-digit'
                          })}
                        </p>
                      </div>
                      
                      {appointment.doctor && (
                        <div>
                          <span className="font-medium text-gray-700">Doctor:</span>
                          <p className="text-gray-900">{appointment.doctor.name}</p>
                          {appointment.doctor.specialty && (
                            <p className="text-gray-600 text-xs">{appointment.doctor.specialty}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                  
                  <div className="text-right">
                    <p className="text-xs text-gray-500">
                      Created: {new Date(appointment.createdAt).toLocaleDateString()}
                    </p>
                    {appointment.updatedAt && appointment.updatedAt !== appointment.createdAt && (
                      <p className="text-xs text-gray-500">
                        Updated: {new Date(appointment.updatedAt).toLocaleDateString()}
                      </p>
                    )}
                  </div>
                </div>
              </div>
            ))}
        </div>
      )}
    </div>
  )
}
