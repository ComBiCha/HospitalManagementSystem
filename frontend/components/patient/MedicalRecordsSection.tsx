'use client';

import { useState, useEffect } from 'react';
import { api } from '@/lib/api';
import { MedicalRecord } from '@/lib/types';
import toast from 'react-hot-toast';
import { FileText, Calendar, User, Stethoscope, ChevronDown, ChevronUp, AlertCircle, DollarSign } from 'lucide-react';

interface MedicalRecordsSectionProps {
  patientId: number | null;
}

export default function MedicalRecordsSection({ patientId }: MedicalRecordsSectionProps) {
  const [records, setRecords] = useState<MedicalRecord[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [expandedRecordId, setExpandedRecordId] = useState<number | null>(null);

  useEffect(() => {
    if (patientId) {
      fetchMedicalRecords();
    }
  }, [patientId]);

  const fetchMedicalRecords = async () => {
    if (!patientId) return;
    setIsLoading(true);
    try {
      const response = await api.get(`/patient-portal/medical-records`);
      setRecords(response.data || []);
    } catch (error) {
      console.error('Error fetching medical records:', error);
      toast.error('Không thể tải hồ sơ bệnh án.');
    } finally {
      setIsLoading(false);
    }
  };

  const toggleRecord = (id: number) => {
    setExpandedRecordId(expandedRecordId === id ? null : id);
  };

  const parseJsonString = (jsonString: string, fallback: any[] = []) => {
    try {
      return JSON.parse(jsonString) || fallback;
    } catch (e) {
      return fallback;
    }
  };

  if (isLoading) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
        <p className="mt-4 text-gray-500">Đang tải hồ sơ...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Hồ sơ bệnh án</h2>
        <p className="text-gray-600 mt-1">Xem lại lịch sử khám và các chi tiết điều trị của bạn.</p>
      </div>

      {records.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <FileText className="mx-auto h-16 w-16 text-gray-400 mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ</h3>
          <p className="text-gray-500">Hồ sơ bệnh án của bạn sẽ xuất hiện ở đây sau các lần khám.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {records.map((record) => {
            const isExpanded = expandedRecordId === record.id;
            const diagnosisList = parseJsonString(record.diagnosis);
            const prescriptionList = parseJsonString(record.prescription);

            return (
              <div key={record.id} className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <button
                  onClick={() => toggleRecord(record.id)}
                  className="w-full text-left p-6 hover:bg-gray-50 focus:outline-none"
                >
                  <div className="flex justify-between items-center">
                    <div className="flex items-center space-x-4">
                      <div className="p-3 bg-blue-100 rounded-lg">
                        <FileText className="w-6 h-6 text-blue-600" />
                      </div>
                      <div>
                        <p className="font-bold text-gray-900">Hồ sơ ngày {new Date(record.appointmentDate).toLocaleDateString('vi-VN')}</p>
                        <p className="text-sm text-gray-600">Bác sĩ: {record.doctorName}</p>
                      </div>
                    </div>
                    <div className="flex items-center space-x-4">
                      <span className={`px-3 py-1 text-xs font-semibold rounded-full ${record.appointmentStatus === 'Completed' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'}`}>
                        {record.appointmentStatus}
                      </span>
                      {isExpanded ? <ChevronUp className="w-5 h-5 text-gray-500" /> : <ChevronDown className="w-5 h-5 text-gray-500" />}
                    </div>
                  </div>
                </button>

                {isExpanded && (
                  <div className="border-t border-gray-200 p-6 space-y-6 bg-gray-50">
                    <div>
                      <h4 className="font-semibold text-gray-800 mb-2">Chẩn đoán</h4>
                      <ul className="list-disc list-inside space-y-1 text-gray-700">
                        {diagnosisList.map((d: any, i: number) => <li key={i}>{d.name} ({d.code})</li>)}
                      </ul>
                    </div>

                    <div>
                      <h4 className="font-semibold text-gray-800 mb-2">Phương pháp điều trị</h4>
                      <p className="text-gray-700 whitespace-pre-wrap">{record.treatment || 'Chưa có phương pháp điều trị.'}</p>
                    </div>

                    <div>
                      <h4 className="font-semibold text-gray-800 mb-2">Đơn thuốc và Xét nghiệm</h4>
                      <div className="space-y-2">
                        {prescriptionList
                          .filter((p: any) => p.status !== 'Cancelled' && p.status !== 'CancelRequested')
                          .map((p: any, i: number) => (
                            <div key={i} className={`p-3 border rounded-md text-sm ${
                              p.type === 'drug' ? 'bg-pink-50 border-pink-200' : 'bg-blue-50 border-blue-200'
                            }`}>
                              <div className="flex justify-between items-center">
                                <p className={`font-medium ${
                                  p.type === 'drug' ? 'text-pink-700' : 'text-blue-700'
                                }`}>
                                  {p.name} {p.quantity ? `(Số lượng: ${p.quantity})` : ''}
                                </p>
                                <span className={`px-2 py-0.5 text-xs font-semibold rounded-full ${
                                    p.status === 'Completed' ? 'bg-teal-100 text-teal-800' :
                                    p.status === 'Confirmed' ? 'bg-green-100 text-green-800' :
                                    'bg-gray-100 text-gray-800'
                                }`}>
                                  {p.status}
                                </span>
                              </div>
                            </div>
                          ))}
                      </div>
                    </div>
                    <div>
                      <h4 className="font-semibold text-gray-800 mb-2">Ghi chú của bác sĩ</h4>
                      <p className="text-gray-700 whitespace-pre-wrap">{record.notes || 'Không có ghi chú.'}</p>
                    </div>
                    <div className="border-t pt-4">
                      <h4 className="font-semibold text-gray-800 mb-2 flex items-center"><DollarSign className="w-4 h-4 mr-2"/>Chi phí</h4>
                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between"><span>Phí khám:</span><span className="font-medium">{record.consultationFee.toLocaleString()}đ</span></div>
                        <div className="flex justify-between"><span>Phí thuốc:</span><span className="font-medium">{record.medicineFee.toLocaleString()}đ</span></div>
                        <div className="flex justify-between"><span>Phí xét nghiệm:</span><span className="font-medium">{record.testFee.toLocaleString()}đ</span></div>
                        <div className="flex justify-between"><span>Phí khác:</span><span className="font-medium">{record.otherFee.toLocaleString()}đ</span></div>
                        <div className="flex justify-between font-bold border-t pt-2 mt-2"><span>Tổng cộng:</span><span>{record.totalFee.toLocaleString()}đ</span></div>
                        <div className="flex justify-between"><span>Đã thanh toán:</span><span className="text-green-600 font-medium">{record.paidAmount.toLocaleString()}đ</span></div>
                        <div className="flex justify-between"><span>Còn lại:</span><span className="text-red-600 font-medium">{(record.totalFee - record.paidAmount).toLocaleString()}đ</span></div>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
