import { useState, useEffect } from 'react';
import { api } from '@/lib/api';
import { PatientIdentifier, PatientIdentifierFormData } from '@/lib/types';
import toast from 'react-hot-toast';

interface IdentifiersSectionProps {
  patientId: number | null;
}

export default function IdentifiersSection({ patientId }: IdentifiersSectionProps) {
  const [identifiers, setIdentifiers] = useState<PatientIdentifier[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isAdding, setIsAdding] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<PatientIdentifierFormData>({
    ehrSystem: NaN,
    externalId: '',
    identifierType: '',
  });

  useEffect(() => {
    if (patientId) {
      fetchIdentifiers();
    }
  }, [patientId]);

  const fetchIdentifiers = async () => {
    if (!patientId) return;
    setIsLoading(true);
    try {
      const response = await api.get(`/patients/${patientId}/identifiers`);
      setIdentifiers(response.data || []);
    } catch (error) {
      console.error('Error fetching identifiers:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleAdd = async () => {
    if (!patientId) return;
    setIsLoading(true);

    if (formData.ehrSystem === 0) {
      try {
        const response = await api.get('/patients/verify-ehr-id', {
          params: {
            ehrSystem: 'Epic',
            patientId: formData.externalId,
          },
        });

        if (!response.data.isValid) {
          toast.error("Không tìm thấy bệnh nhân trên hệ thống Epic.");
          setIsLoading(false);
          return;
        }
      } catch (error) {
        console.error("Error verifying identifier:", error);
        toast.error("Lỗi khi kiểm tra định danh trên Epic.");
        setIsLoading(false);
        return;
      }
    }

    try {
      const payload = {
        EHRSystem: formData.ehrSystem,
        ExternalId: formData.externalId,
        IdentifierType: formData.identifierType,
      };
      const response = await api.post(`/patients/${patientId}/identifiers`, payload);
      setIdentifiers([...identifiers, response.data]);
      setFormData({ ehrSystem: NaN, externalId: '', identifierType: '' });
      setIsAdding(false);
      toast.success('Thêm định danh thành công!');
    } catch (error) {
      console.error('Error adding identifier:', error);
      toast.error('Thêm định danh thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  const handleUpdate = async (id: number) => {
    if (!patientId) return;
    setIsLoading(true);

    if (formData.ehrSystem === 0) {
      try {
        const response = await api.get('/patients/verify-ehr-id', {
          params: {
            ehrSystem: 'Epic',
            patientId: formData.externalId,
          },
        });

        if (!response.data.isValid) {
          toast.error("Không tìm thấy bệnh nhân trên hệ thống Epic.");
          setIsLoading(false);
          return;
        }
      } catch (error) {
        console.error("Error verifying identifier:", error);
        toast.error("Lỗi khi kiểm tra định danh trên Epic.");
        setIsLoading(false);
        return;
      }
    }

    try {
      const payload = {
        Id: id,
        EHRSystem: formData.ehrSystem,
        ExternalId: formData.externalId,
        IdentifierType: formData.identifierType,
        PatientId: patientId,
      };
      const response = await api.put(`/patients/identifiers/${id}`, payload);
      setIdentifiers(identifiers.map((item) => (item.id === id ? response.data : item)));
      setEditingId(null);
      toast.success('Cập nhật định danh thành công!');
    } catch (error) {
      console.error('Error updating identifier:', error);
      toast.error('Cập nhật định danh thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Bạn có chắc chắn muốn xóa định danh này?')) return;
    setIsLoading(true);
    try {
      await api.delete(`/patients/identifiers/${id}`);
      setIdentifiers(identifiers.filter((item) => item.id !== id));
      toast.success('Xóa định danh thành công!');
    } catch (error) {
      console.error('Error deleting identifier:', error);
      toast.error('Xóa định danh thất bại!');
    } finally {
      setIsLoading(false);
    }
  };

  const startEdit = (identifier: PatientIdentifier) => {
    setEditingId(identifier.id);
    setFormData({
      ehrSystem: typeof identifier.ehrSystem === 'number' ? identifier.ehrSystem : parseInt(identifier.ehrSystem, 10),
      externalId: identifier.externalId,
      identifierType: identifier.identifierType,
    });
  };

  const cancelEdit = () => {
    setEditingId(null);
    setFormData({ ehrSystem: NaN, externalId: '', identifierType: '' });
  };

  const getEHRSystemName = (ehrSystem: string | number): string => {
    const systemMap: Record<number, string> = {
      0: 'Epic',
      1: 'Cerner',
      2: 'MEDITECH',
      3: 'Allscripts',
      99: 'Other',
    };
    const systemNumber = typeof ehrSystem === 'number' ? ehrSystem : parseInt(ehrSystem, 10);
    return systemMap[systemNumber] || `Unknown (${ehrSystem})`;
  };

  if (!patientId) {
    return (
      <div className="space-y-6">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Định danh y tế</h2>
          <p className="text-gray-600 mt-1">Quản lý định danh y tế trên các hệ thống EHR</p>
        </div>
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ bệnh nhân</h3>
          <p className="text-gray-500">Vui lòng tạo hồ sơ bệnh nhân trước khi thêm định danh.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Định danh y tế</h2>
        <p className="text-gray-600 mt-1">Quản lý định danh y tế trên các hệ thống EHR</p>
      </div>

      {/* Add Button */}
      {!isAdding && (
        <button
          onClick={() => setIsAdding(true)}
          className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
        >
          <span className="mr-2">+</span>
          Thêm định danh mới
        </button>
      )}

      {/* Add Form */}
      {isAdding && (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">Thêm định danh mới</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Hệ thống EHR</label>
              <select
                value={isNaN(formData.ehrSystem) ? '' : formData.ehrSystem}
                onChange={(e) => setFormData({ ...formData, ehrSystem: Number(e.target.value) })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Chọn hệ thống</option>
                <option value={0}>Epic</option>
                <option value={1}>Cerner</option>
                <option value={2}>MEDITECH</option>
                <option value={3}>Allscripts</option>
                <option value={99}>Other</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Mã định danh</label>
              <input
                type="text"
                value={formData.externalId}
                onChange={(e) => setFormData({ ...formData, externalId: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="Nhập mã định danh"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Loại định danh</label>
              <select
                value={formData.identifierType}
                onChange={(e) => setFormData({ ...formData, identifierType: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Chọn loại</option>
                <option value="FHIR">FHIR</option>
                <option value="SSN">Số an sinh xã hội</option>
                <option value="Insurance">Mã bảo hiểm</option>
                <option value="National">Mã quốc gia</option>
                <option value="Other">Khác</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end space-x-3 mt-4">
            <button
              onClick={() => {
                setIsAdding(false);
                setFormData({ ehrSystem: NaN, externalId: '', identifierType: '' });
              }}
              className="text-gray-600 hover:text-gray-800 font-medium text-sm"
              disabled={isLoading}
            >
              Hủy
            </button>
            <button
              onClick={handleAdd}
              disabled={isLoading || isNaN(formData.ehrSystem) || !formData.externalId || !formData.identifierType}
              className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 text-sm disabled:opacity-50"
            >
              {isLoading ? 'Đang thêm...' : 'Thêm'}
            </button>
          </div>
        </div>
      )}

      {/* Identifiers List */}
      {isLoading && identifiers.length === 0 ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      ) : identifiers.length === 0 && !isAdding ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="text-gray-400 mb-4">
            <svg className="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V8a2 2 0 00-2-2h-5m-4 0V5a2 2 0 114 0v1m-4 0a2 2 0 104 0m-5 8a2 2 0 100-4 2 2 0 000 4zm0 0c1.306 0 2.417.835 2.83 2M9 14a3.001 3.001 0 00-2.83 2M15 11h3m-3 4h2" />
            </svg>
          </div>
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có định danh y tế</h3>
          <p className="text-gray-500">Bạn chưa có định danh y tế nào được liên kết.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {identifiers.map((identifier) => (
            <div key={identifier.id} className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
              {editingId === identifier.id ? (
                <div>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">Hệ thống EHR</label>
                      <select
                        value={isNaN(formData.ehrSystem) ? '' : formData.ehrSystem}
                        onChange={(e) => setFormData({ ...formData, ehrSystem: Number(e.target.value) })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        <option value={0}>Epic</option>
                        <option value={1}>Cerner</option>
                        <option value={2}>MEDITECH</option>
                        <option value={3}>Allscripts</option>
                        <option value={99}>Other</option>
                      </select>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">Mã định danh</label>
                      <input
                        type="text"
                        value={formData.externalId}
                        onChange={(e) => setFormData({ ...formData, externalId: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">Loại</label>
                      <select
                        value={formData.identifierType}
                        onChange={(e) => setFormData({ ...formData, identifierType: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        <option value="FHIR">FHIR</option>
                        <option value="SSN">SSN</option>
                        <option value="Insurance">Insurance</option>
                        <option value="National">National</option>
                        <option value="Other">Other</option>
                      </select>
                    </div>
                  </div>
                  <div className="flex justify-end space-x-3">
                    <button
                      onClick={cancelEdit}
                      className="text-gray-600 hover:text-gray-800 font-medium text-sm"
                      disabled={isLoading}
                    >
                      Hủy
                    </button>
                    <button
                      onClick={() => handleUpdate(identifier.id)}
                      disabled={isLoading}
                      className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 text-sm disabled:opacity-50"
                    >
                      {isLoading ? 'Đang lưu...' : 'Lưu'}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex items-start justify-between">
                  <div className="flex-1 grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1">Hệ thống EHR</label>
                      <p className="text-gray-900 font-semibold">{getEHRSystemName(identifier.ehrSystem)}</p>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1">Mã định danh</label>
                      <p className="text-gray-900">{identifier.externalId}</p>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1">Loại</label>
                      <p className="text-gray-900">{identifier.identifierType}</p>
                    </div>
                  </div>
                  <div className="flex items-center space-x-3 ml-4">
                    <span className={`inline-flex px-3 py-1 text-xs font-semibold rounded-full ${identifier.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                      {identifier.isActive ? 'Hoạt động' : 'Không hoạt động'}
                    </span>
                    <button
                      onClick={() => startEdit(identifier)}
                      className="text-blue-600 hover:text-blue-800 text-sm font-medium"
                    >
                      Sửa
                    </button>
                    <button
                      onClick={() => handleDelete(identifier.id)}
                      className="text-red-600 hover:text-red-800 text-sm font-medium"
                    >
                      Xóa
                    </button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}