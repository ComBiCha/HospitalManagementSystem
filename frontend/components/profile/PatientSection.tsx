'use client';

import { useState } from 'react'
import { Patient, PatientFormData } from '@/lib/types'

interface PatientSectionProps {
  patient: Patient | null
  onUpdate: (data: PatientFormData) => Promise<void>
  onCreate: (data: PatientFormData) => Promise<void>
}

export default function PatientSection({ patient, onUpdate, onCreate }: PatientSectionProps) {
  const [isEditing, setIsEditing] = useState(false)
  const [isCreating, setIsCreating] = useState(!patient)
  const [formData, setFormData] = useState<PatientFormData>({
    name: patient?.name || '',
    age: patient?.age || 0,
    email: patient?.email || ''
  })
  const [isLoading, setIsLoading] = useState(false)

  const handleSave = async () => {
    setIsLoading(true)
    try {
      if (isCreating) {
        await onCreate(formData)
        setIsCreating(false)
      } else {
        await onUpdate(formData)
        setIsEditing(false)
      }
    } catch (error) {
      console.error('Failed to save patient:', error)
    } finally {
      setIsLoading(false)
    }
  }

  const handleCancel = () => {
    if (patient) {
      setFormData({
        name: patient.name,
        age: patient.age,
        email: patient.email
      })
      setIsEditing(false)
    } else {
      setFormData({
        name: '',
        age: 0,
        email: ''
      })
      setIsCreating(false)
    }
  }

  const showForm = isEditing || isCreating

  return (
    <div className="bg-white rounded-lg border shadow-sm p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Patient Information</h2>
        {!showForm ? (
          <div className="space-x-3">
            {patient ? (
              <button
                onClick={() => setIsEditing(true)}
                className="text-blue-600 hover:text-blue-800 font-medium"
              >
                Edit
              </button>
            ) : (
              <button
                onClick={() => setIsCreating(true)}
                className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700"
              >
                Create Patient Profile
              </button>
            )}
          </div>
        ) : (
          <div className="space-x-3">
            <button
              onClick={handleCancel}
              className="text-gray-600 hover:text-gray-800 font-medium"
              disabled={isLoading}
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              disabled={isLoading}
              className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 disabled:opacity-50"
            >
              {isLoading ? 'Saving...' : (isCreating ? 'Create' : 'Save')}
            </button>
          </div>
        )}
      </div>

      {!patient && !isCreating ? (
        <div className="text-center py-8">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>
          <h3 className="text-sm font-medium text-gray-900 mb-1">No patient profile</h3>
          <p className="text-sm text-gray-500">Get started by creating a patient profile.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Patient Name
            </label>
            {showForm ? (
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Enter patient name"
              />
            ) : (
              <p className="text-gray-900 py-2">{patient?.name}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Age
            </label>
            {showForm ? (
              <input
                type="number"
                value={formData.age}
                onChange={(e) => setFormData({ ...formData, age: parseInt(e.target.value) || 0 })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Enter age"
                min="0"
                max="150"
              />
            ) : (
              <p className="text-gray-900 py-2">{patient?.age}</p>
            )}
          </div>

          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Email
            </label>
            {showForm ? (
              <input
                type="email"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Enter email address"
              />
            ) : (
              <p className="text-gray-900 py-2">{patient?.email}</p>
            )}
          </div>

          {patient && !showForm && (
            <>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Status
                </label>
                <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                  patient.status === 'Active' 
                    ? 'bg-green-100 text-green-800' 
                    : 'bg-yellow-100 text-yellow-800'
                }`}>
                  {patient.status}
                </span>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Patient ID
                </label>
                <p className="text-gray-900 py-2">#{patient.id}</p>
              </div>
            </>
          )}
        </div>
      )}

      {patient && !showForm && (
        <div className="mt-6 pt-6 border-t border-gray-200">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-sm text-gray-600">
            <div>
              <span className="font-medium">Created:</span> {new Date(patient.createdAt).toLocaleDateString()}
            </div>
            {patient.updatedAt && (
              <div>
                <span className="font-medium">Last Updated:</span> {new Date(patient.updatedAt).toLocaleDateString()}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
