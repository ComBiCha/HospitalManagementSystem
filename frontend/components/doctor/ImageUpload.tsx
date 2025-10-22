'use client';

import { useState } from 'react';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { UploadCloud, X } from 'lucide-react';

interface ImageUploadProps {
  medicalRecordId: number;
  onUploadSuccess: () => void; // Callback to refresh the image list
}

export default function ImageUpload({ medicalRecordId, onUploadSuccess }: ImageUploadProps) {
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState('');
  const [imageType, setImageType] = useState('dicom'); // Default to dicom
  const [isUploading, setIsUploading] = useState(false);

  const handleUpload = async () => {
    if (!file) {
      toast.error('Vui lòng chọn một file để tải lên.');
      return;
    }
    setIsUploading(true);
    toast.loading('Đang tải lên...');

    const formData = new FormData();
    formData.append('medicalRecordId', medicalRecordId.toString());
    formData.append('imageType', imageType);
    formData.append('description', description);
    formData.append('file', file);

    try {
      await api.post('/dicom/upload', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });
      toast.dismiss();
      toast.success('Tải lên thành công!');
      onUploadSuccess(); // Trigger refresh
      setShowUploadModal(false);
      // Reset form
      setFile(null);
      setDescription('');
      setImageType('dicom');
    } catch (error) {
      console.error('Error uploading image:', error);
      toast.dismiss();
      toast.error('Lỗi khi tải lên hình ảnh.');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <>
      <div className="bg-white rounded-2xl shadow-xl p-6">
        <div className="flex justify-between items-center">
            <h3 className="text-lg font-bold text-gray-900 flex items-center space-x-2">
                <UploadCloud className="w-5 h-5 text-emerald-600" />
                <span>Tải lên Hình ảnh</span>
            </h3>
            <button 
                onClick={() => setShowUploadModal(true)} 
                className="px-4 py-2 bg-emerald-600 text-white rounded-xl hover:bg-emerald-700 transition-colors font-semibold flex items-center justify-center space-x-2 shadow-lg"
            >
                Tải lên
            </button>
        </div>
      </div>

      {/* Upload Modal */}
      {showUploadModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-lg w-full" onClick={(e) => e.stopPropagation()}>
            <div className="p-6 border-b flex items-center justify-between">
                <h3 className="text-xl font-bold text-gray-900">Tải lên Hình ảnh mới</h3>
                <button onClick={() => setShowUploadModal(false)} className="p-2 hover:bg-gray-200 rounded-full transition-colors">
                    <X className="w-6 h-6 text-gray-600" />
                </button>
            </div>
            <div className="p-6 space-y-4">
                <div>
                    <label className="text-sm font-semibold text-gray-500 mb-1 block">File</label>
                    <input 
                        type="file" 
                        onChange={(e) => setFile(e.target.files ? e.target.files[0] : null)} 
                        className="w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-emerald-50 file:text-emerald-700 hover:file:bg-emerald-100" 
                    />
                </div>
                 <div>
                    <label className="text-sm font-semibold text-gray-500 mb-1 block">Loại hình ảnh</label>
                    <select 
                        value={imageType} 
                        onChange={(e) => setImageType(e.target.value)} 
                        className="w-full p-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    >
                        <option value="dicom">DICOM</option>
                        <option value="xray">X-Quang</option>
                        <option value="scan">Scan</option>
                        <option value="photo">Ảnh chụp</option>
                        <option value="other">Khác</option>
                    </select>
                </div>
                <div>
                    <label className="text-sm font-semibold text-gray-500 mb-1 block">Mô tả</label>
                    <textarea 
                        placeholder="Ví dụ: X-quang ngực thẳng..." 
                        value={description} 
                        onChange={(e) => setDescription(e.target.value)} 
                        className="w-full p-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none" 
                        rows={3}
                    />
                </div>
            </div>
            <div className="bg-gray-50 p-6 rounded-b-2xl flex justify-end space-x-3">
                <button onClick={() => setShowUploadModal(false)} className="px-6 py-2 bg-gray-200 text-gray-700 rounded-xl hover:bg-gray-300 transition-colors font-semibold">
                    Hủy
                </button>
                <button onClick={handleUpload} disabled={isUploading} className="px-6 py-2 bg-emerald-600 text-white rounded-xl hover:bg-emerald-700 transition-colors font-semibold disabled:opacity-50 disabled:cursor-not-allowed">
                    {isUploading ? 'Đang tải...' : 'Tải lên'}
                </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
