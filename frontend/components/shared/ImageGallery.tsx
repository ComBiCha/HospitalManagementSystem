'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import toast from 'react-hot-toast';
import { Image as ImageIcon, Download, X } from 'lucide-react';

// A new component to handle fetching and displaying an image with authentication.
const AuthenticatedImage = ({ imageId, alt, className }: { imageId: number, alt: string, className: string }) => {
  const [imageUrl, setImageUrl] = useState<string>(''); // Start with empty

  useEffect(() => {
    let objectUrl: string | null = null;

    const fetchImagePreview = async () => {
      if (!imageId) return;
      try {
        const response = await api.get(`/dicom/${imageId}/preview`, {
          responseType: 'blob',
        });
        objectUrl = URL.createObjectURL(response.data);
        setImageUrl(objectUrl);
      } catch (error) {
        console.error(`Error fetching preview for image ${imageId}:`, error);
        setImageUrl('/placeholder-image.svg'); // Fallback on error
      }
    };

    fetchImagePreview();

    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [imageId]);

  if (!imageUrl) {
    // Optional: show a loading spinner while fetching
    return (
      <div className={`${className} flex items-center justify-center bg-gray-200`}>
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600"></div>
      </div>
    );
  }

  return (
    <img
      src={imageUrl}
      alt={alt}
      className={className}
      onError={(e) => { 
        // If the object URL fails for some reason, switch to placeholder
        if (e.currentTarget.src !== '/placeholder-image.svg') {
          e.currentTarget.src = '/placeholder-image.svg';
        }
      }}
    />
  );
};


interface ImageInfo {
  id: number;
  description: string;
  originalFileName: string;
  contentType: string;
  imageType: string;
  uploadedAt: string;
}

interface ImageGalleryProps {
  medicalRecordId: number;
}

export default function ImageGallery({ medicalRecordId }: ImageGalleryProps) {
  const [images, setImages] = useState<ImageInfo[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedImage, setSelectedImage] = useState<ImageInfo | null>(null);

  useEffect(() => {
    if (medicalRecordId) {
      fetchImages();
    }
  }, [medicalRecordId]);

  const fetchImages = async () => {
    setIsLoading(true);
    try {
      const response = await api.get(`/dicom/medical-record/${medicalRecordId}`);
      setImages(response.data || []);
    } catch (error) {
      console.error('Error fetching images:', error);
      toast.error('Không thể tải danh sách hình ảnh.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDownload = async (e: React.MouseEvent, image: ImageInfo) => {
    e.stopPropagation(); // Prevent modal from opening
    toast.loading('Đang tải xuống DICOM...');
    try {
      const response = await api.get(`/dicom/${image.id}/download`, {
        responseType: 'blob',
      });
      const url = window.URL.createObjectURL(new Blob([response.data], { type: image.contentType }));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', image.originalFileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      toast.dismiss();
      toast.success('Tải xuống DICOM thành công!');
    } catch (error) {
      console.error('Error downloading image:', error);
      toast.dismiss();
      toast.error('Lỗi khi tải xuống file DICOM.');
    }
  };

  const handleDownloadPreview = async (e: React.MouseEvent, image: ImageInfo) => {
    e.stopPropagation();
    toast.loading('Đang tải xuống preview...');
    try {
      const response = await api.get(`/dicom/${image.id}/preview`, {
        responseType: 'blob',
      });
      
      const originalFileName = image.originalFileName || 'download';
      const extensionIndex = originalFileName.lastIndexOf('.');
      const baseName = extensionIndex !== -1 ? originalFileName.substring(0, extensionIndex) : originalFileName;
      const previewFileName = `${baseName}_preview.jpg`;

      const url = window.URL.createObjectURL(new Blob([response.data], { type: 'image/jpeg' }));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', previewFileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      toast.dismiss();
      toast.success('Tải xuống preview thành công!');
    } catch (error) {
      console.error('Error downloading image preview:', error);
      toast.dismiss();
      toast.error('Lỗi khi tải xuống preview.');
    }
  };

  return (
    <div className="bg-white rounded-2xl shadow-xl p-6">
      <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
        <ImageIcon className="w-5 h-5 text-emerald-600" />
        <span>Hình ảnh</span>
      </h3>
      {isLoading ? (
        <div className="text-center text-gray-500">Đang tải...</div>
      ) : images.length === 0 ? (
        <div className="text-center text-gray-500 italic py-4">Chưa có hình ảnh nào.</div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {images.map((image) => (
            <div
              key={image.id}
              onClick={() => setSelectedImage(image)}
              className="relative group aspect-square border rounded-lg overflow-hidden cursor-pointer hover:border-emerald-500 transition-all duration-300 shadow-sm"
            >
              <AuthenticatedImage
                imageId={image.id}
                alt={image.description || 'Image'}
                className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-110"
              />
              <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex items-center justify-center p-2">
                <p className="text-white text-center text-xs font-semibold">{image.description || image.originalFileName}</p>
              </div>
              <div className="absolute top-2 right-2 bg-black/50 text-white text-xs px-2 py-1 rounded-full">
                {image.imageType}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Image Detail Modal */}
      {selectedImage && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={() => setSelectedImage(null)}>
          <div className="bg-white rounded-2xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
            <div className="p-6 border-b flex items-center justify-between">
              <h3 className="text-xl font-bold text-gray-900">Chi tiết Hình ảnh</h3>
              <button onClick={() => setSelectedImage(null)} className="p-2 hover:bg-gray-200 rounded-full transition-colors">
                <X className="w-6 h-6 text-gray-600" />
              </button>
            </div>
            <div className="p-6 flex-grow overflow-y-auto grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="bg-gray-100 rounded-lg flex items-center justify-center aspect-square overflow-hidden">
                <AuthenticatedImage
                  imageId={selectedImage.id}
                  alt={selectedImage.description || 'Preview'}
                  className="max-w-full max-h-full object-contain"
                />
              </div>
              <div className="space-y-4">
                <div>
                  <label className="text-sm font-semibold text-gray-500">Mô tả</label>
                  <p className="text-gray-800 font-medium">{selectedImage.description || 'N/A'}</p>
                </div>
                <div>
                  <label className="text-sm font-semibold text-gray-500">Tên file gốc</label>
                  <p className="text-gray-800 font-medium">{selectedImage.originalFileName}</p>
                </div>
                <div>
                  <label className="text-sm font-semibold text-gray-500">Loại hình ảnh</label>
                  <p className="text-gray-800 font-medium">{selectedImage.imageType}</p>
                </div>
                <div>
                  <label className="text-sm font-semibold text-gray-500">Ngày tải lên</label>
                  <p className="text-gray-800 font-medium">{new Date(selectedImage.uploadedAt).toLocaleString('vi-VN')}</p>
                </div>
                <div className="pt-4 space-y-3">
                  <button
                    onClick={(e) => handleDownload(e, selectedImage)}
                    className="w-full px-4 py-3 bg-emerald-600 text-white rounded-xl hover:bg-emerald-700 transition-colors font-semibold flex items-center justify-center space-x-2 shadow-lg"
                  >
                    <Download className="w-5 h-5" />
                    <span>Tải xuống DICOM</span>
                  </button>
                  <button
                    onClick={(e) => handleDownloadPreview(e, selectedImage)}
                    className="w-full px-4 py-3 bg-blue-600 text-white rounded-xl hover:bg-blue-700 transition-colors font-semibold flex items-center justify-center space-x-2 shadow-lg"
                  >
                    <Download className="w-5 h-5" />
                    <span>Tải xuống Preview</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}