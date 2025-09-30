'use client';

import { useState } from 'react'
import { PatientIdentifier, PatientIdentifierFormData } from '@/lib/types'

interface IdentifiersSectionProps {
  identifiers: PatientIdentifier[]
  onAdd: (data: PatientIdentifierFormData) => Promise<void>
  onUpdate: (id: number, data: PatientIdentifierFormData) => Promise<void>
  onDelete: (id: number) => Promise<void>
}

export default function IdentifiersSection({ identifiers, onAdd, onUpdate, onDelete }: IdentifiersSectionProps) {
  const [isAdding, setIsAdding] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [formData, setFormData] = useState<PatientIdentifierFormData>({
    ehrSystem: '',
    externalId: '',
    identifierType: ''
  })
  const [isLoading, setIsLoading] = useState(false)

  const handleAdd = async () => {
    setIsLoading(true)
    try {
      await onAdd(formData)
      setFormData({ ehrSystem: '', externalId: '', identifierType: '' })
      setIsAdding(false)
    } catch (error) {
      console.error('Failed to add identifier:', error)
    } finally {
      setIsLoading(false)
    }
  }

  const handleUpdate = async (id: number) => {
    setIsLoading(true)
    try {
      await onUpdate(id, formData)
      setEditingId(null)
    } catch (error) {
      console.error('Failed to update identifier:', error)
    } finally {
      setIsLoading(false)
    }
  }

  const handleDelete = async (id: number) => {
    if (confirm('Are you sure you want to delete this identifier?')) {
      setIsLoading(true)
      try {
        await onDelete(id)
      } catch (error) {
        console.error('Failed to delete identifier:', error)
      } finally {
        setIsLoading(false)
      }
    }
  }

  const startEdit = (identifier: PatientIdentifier) => {
    setEditingId(identifier.id)
    setFormData({
      ehrSystem: identifier.ehrSystem,
      externalId: identifier.externalId,
      identifierType: identifier.identifierType
    })
  }

  const cancelEdit = () => {
    setEditingId(null)
    setFormData({ ehrSystem: '', externalId: '', identifierType: '' })
  }

  const cancelAdd = () => {
    setIsAdding(false)
    setFormData({ ehrSystem: '', externalId: '', identifierType: '' })
  }

  return (
    <div className="bg-white rounded-lg border shadow-sm p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Patient Identifiers</h2>
        {!isAdding && (
          <button
            onClick={() => setIsAdding(true)}
            className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700"
          >
            Add Identifier
          </button>
        )}
      </div>

      {/* Add New Identifier Form */}
      {isAdding && (
        <div className="mb-6 p-4 border border-gray-200 rounded-md bg-gray-50">
          <h3 className="text-lg font-medium text-gray-900 mb-4">Add New Identifier</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                EHR System
              </label>
              <select
                value={formData.ehrSystem}
                onChange={(e) => setFormData({ ...formData, ehrSystem: Number(e.target.value) })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Select EHR System</option>
                <option value={0}>Epic</option>
                <option value={1}>Cerner</option>
                <option value={2}>MEDITECH</option>
                <option value={3}>Allscripts</option>
                <option value={99}>Other</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                External ID
              </label>
              <input
                type="text"
                value={formData.externalId}
                onChange={(e) => setFormData({ ...formData, externalId: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Enter external ID"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Identifier Type
              </label>
              <select
                value={formData.identifierType}
                onChange={(e) => setFormData({ ...formData, identifierType: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Select Type</option>
                <option value="FHIR">FHIR</option>
                <option value="SSN">Social Security Number</option>
                <option value="Insurance">Insurance ID</option>
                <option value="National">National ID</option>
                <option value="Other">Other</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end space-x-3 mt-4">
            <button
              onClick={cancelAdd}
              className="text-gray-600 hover:text-gray-800 font-medium"
              disabled={isLoading}
            >
              Cancel
            </button>
            <button
              onClick={handleAdd}
              disabled={isLoading || !String(formData.ehrSystem) || !formData.externalId || !formData.identifierType}
              className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 disabled:opacity-50"
            >
              {isLoading ? 'Adding...' : 'Add Identifier'}
            </button>
          </div>
        </div>
      )}

      {/* Identifiers List */}
      {identifiers.length === 0 && !isAdding ? (
        <div className="text-center py-8">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          </div>
          <h3 className="text-sm font-medium text-gray-900 mb-1">No identifiers</h3>
          <p className="text-sm text-gray-500">Add patient identifiers to link with external systems.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {identifiers.map((identifier) => (
            <div key={identifier.id} className="border border-gray-200 rounded-md p-4">
              {editingId === identifier.id ? (
                <div>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        EHR System
                      </label>
                      <select
                        value={formData.ehrSystem}
                        onChange={(e) => setFormData({ ...formData, ehrSystem: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        <option value="Epic">Epic</option>
                        <option value="Cerner">Cerner</option>
                        <option value="Meditech">MEDITECH</option>
                        <option value="Allscripts">Allscripts</option>
                        <option value="Other">Other</option>
                      </select>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        External ID
                      </label>
                      <input
                        type="text"
                        value={formData.externalId}
                        onChange={(e) => setFormData({ ...formData, externalId: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        Identifier Type
                      </label>
                      <select
                        value={formData.identifierType}
                        onChange={(e) => setFormData({ ...formData, identifierType: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        <option value="FHIR">FHIR</option>
                        <option value="SSN">Social Security Number</option>
                        <option value="Insurance">Insurance ID</option>
                        <option value="National">National ID</option>
                        <option value="Other">Other</option>
                      </select>
                    </div>
                  </div>
                  <div className="flex justify-end space-x-3">
                    <button
                      onClick={cancelEdit}
                      className="text-gray-600 hover:text-gray-800 font-medium"
                      disabled={isLoading}
                    >
                      Cancel
                    </button>
                    <button
                      onClick={() => handleUpdate(identifier.id)}
                      disabled={isLoading}
                      className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 disabled:opacity-50"
                    >
                      {isLoading ? 'Saving...' : 'Save'}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex justify-between items-start">
                  <div className="grid grid-cols-1 md:grid-cols-4 gap-4 flex-1">
                    <div>
                      <span className="text-sm font-medium text-gray-700">EHR System:</span>
                      <p className="text-gray-900">{identifier.ehrSystem}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-700">External ID:</span>
                      <p className="text-gray-900">{identifier.externalId}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-700">Type:</span>
                      <p className="text-gray-900">{identifier.identifierType}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-700">Status:</span>
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                        identifier.isActive 
                          ? 'bg-green-100 text-green-800' 
                          : 'bg-red-100 text-red-800'
                      }`}>
                        {identifier.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </div>
                  </div>
                  <div className="flex space-x-2 ml-4">
                    <button
                      onClick={() => startEdit(identifier)}
                      className="text-blue-600 hover:text-blue-800 text-sm font-medium"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(identifier.id)}
                      className="text-red-600 hover:text-red-800 text-sm font-medium"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
