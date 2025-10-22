import { useEffect, useState } from 'react';
import { api } from '@/lib/api';

interface DicomImage {
  id: number;
  description: string;
  originalFileName: string;
  contentType: string;
}

interface DicomViewerProps {
  medicalRecordId: number;
  allowUpload?: boolean;
}

export default function DicomViewer({ medicalRecordId, allowUpload = true }: DicomViewerProps) {
  const [images, setImages] = useState<DicomImage[]>([]);
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState('');

  useEffect(() => {
    fetchImages();
  }, [medicalRecordId]);

  const fetchImages = async () => {
    try {
      const response = await api.get(`/dicom/medical-record/${medicalRecordId}`);
      setImages(response.data);
    } catch (error) {
      console.error('Error fetching DICOM images:', error);
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    const formData = new FormData();
    formData.append('medicalRecordId', medicalRecordId.toString());
    formData.append('imageType', 'dicom');
    formData.append('description', description);
    formData.append('file', file);

    try {
      await api.post('/dicom/upload', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });
      setShowUploadModal(false);
      fetchImages();
    } catch (error) {
      console.error('Error uploading DICOM image:', error);
    }
  };

  const handleDownload = async (imageId: number, fileName: string, contentType: string) => {
    try {
      const response = await api.get(`/dicom/${imageId}/download`, {
        responseType: 'blob',
      });
      const url = window.URL.createObjectURL(new Blob([response.data], { type: contentType }));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
    } catch (error) {
      console.error('Error downloading image:', error);
    }
  };

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-lg font-bold text-gray-900">DICOM Images</h3>
        {allowUpload && <button onClick={() => setShowUploadModal(true)} className="bg-blue-500 text-white px-4 py-2 rounded-md">Upload</button>}
      </div>
      <div className="space-y-2">
        {images.map((image) => (
          <div key={image.id} className="flex items-center justify-between p-2 border rounded-md">
            <p>{image.description}</p>
            <button onClick={() => handleDownload(image.id, image.originalFileName, image.contentType)} className="bg-green-500 text-white px-4 py-2 rounded-md">Download</button>
          </div>
        ))}
      </div>

      {showUploadModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center">
          <div className="bg-white p-4 rounded-lg">
            <h3 className="text-lg font-bold text-gray-900 mb-4">Upload DICOM Image</h3>
            <input type="file" onChange={(e) => setFile(e.target.files ? e.target.files[0] : null)} />
            <input type="text" placeholder="Description" value={description} onChange={(e) => setDescription(e.target.value)} className="border p-2 rounded-md w-full mt-2" />
            <div className="flex justify-end mt-4">
              <button onClick={() => setShowUploadModal(false)} className="bg-gray-500 text-white px-4 py-2 rounded-md mr-2">Cancel</button>
              <button onClick={handleUpload} className="bg-blue-500 text-white px-4 py-2 rounded-md">Upload</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
