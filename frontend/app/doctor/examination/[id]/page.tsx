'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import toast from 'react-hot-toast';
import { api } from '@/lib/api';
import {
  User,
  Calendar,
  FileText,
  Pill,
  TestTube,
  DollarSign,
  Save,
  CheckCircle,
  Building2,
  ArrowLeft,
  Search,
  X,
} from 'lucide-react';

interface MedicalRecord {
  id: number;
  appointmentId: number;
  patientId: number;
  doctorId: number;
  diagnosis: string;
  symptoms: string;
  treatment: string;
  prescription: string;
  notes: string;
  consultationFee: number;
  medicineFee: number;
  testFee: number;
  otherFee: number;
  paidAmount: number;
  paymentStatus: string;
  createdAt: string;
  updatedAt: string;
  patient: any;
  doctor: any;
  appointment: any;
}

interface ReferenceItem {
  code?: string;
  name?: string;
  testName?: string;
  description?: string;
  subcategory?: string;
  dosage?: string;
  fee?: number;
  unit?: string;
}

interface PrescriptionItem extends ReferenceItem {
  type: 'drug' | 'test';
  quantity?: number;
  id?: number; // ID from database if already saved
  status?: string; // Pending, Confirmed, CancelRequested, Cancelled
  cancelReason?: string;
  createdAt?: string;
  updatedAt?: string;
}

interface MedicalRecordHistoryItem {
  id: number;
  medicalRecordId: number;
  doctorId: number;
  diagnosis: string;
  symptoms: string;
  treatment: string;
  prescription: string;
  notes: string;
  medicineFee: number;
  testFee: number;
  otherFee: number;
  action: string;
  createdAt: string;
}

const formatDateTimeToVN = (dateString?: string) => {
  if (!dateString) return '';
  try {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('vi-VN', {
      hour: '2-digit',
      minute: '2-digit',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'Asia/Ho_Chi_Minh'
    }).format(date);
  } catch (error) {
    console.error('Error formatting date:', dateString, error);
    return 'Invalid date';
  }
};

export default function ExaminationPage({ params }: { params: { id: string } }) {
  const router = useRouter();
  const [medicalRecord, setMedicalRecord] = useState<MedicalRecord | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isReadOnly, setIsReadOnly] = useState(false);

  // Reference data
  const [diagnoses, setDiagnoses] = useState<ReferenceItem[]>([]);
  const [symptoms, setSymptoms] = useState<ReferenceItem[]>([]);
  const [drugs, setDrugs] = useState<ReferenceItem[]>([]);
  const [tests, setTests] = useState<ReferenceItem[]>([]);

  // Form state
  const [selectedDiagnoses, setSelectedDiagnoses] = useState<ReferenceItem[]>([]);
  const [selectedSymptoms, setSelectedSymptoms] = useState<ReferenceItem[]>([]);
  const [treatment, setTreatment] = useState('');
  const [selectedPrescriptions, setSelectedPrescriptions] = useState<PrescriptionItem[]>([]);
  const [notes, setNotes] = useState('');
  const [otherFee, setOtherFee] = useState(0);

  // Search states
  const [diagnosisSearch, setDiagnosisSearch] = useState('');
  const [symptomSearch, setSymptomSearch] = useState('');
  const [drugSearch, setDrugSearch] = useState('');
  const [testSearch, setTestSearch] = useState('');

  // New states for history and prescription items
  const [showHistoryModal, setShowHistoryModal] = useState(false);
  const [historyItems, setHistoryItems] = useState<MedicalRecordHistoryItem[]>([]);
  const [prescriptionItems, setPrescriptionItems] = useState<PrescriptionItem[]>([]);
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [cancelItemId, setCancelItemId] = useState<number | null>(null);
  const [cancelReason, setCancelReason] = useState('');

  useEffect(() => {
    fetchData();
  }, [params.id]);

  const fetchData = async () => {
    setIsLoading(true);
    try {
      // Fetch medical record
      const recordResponse = await api.get(`/MedicalRecords/${params.id}`);
      const record = recordResponse.data;
      setMedicalRecord(record);

      // Parse existing data
      try {
        setSelectedDiagnoses(JSON.parse(record.diagnosis || '[]'));
      } catch { setSelectedDiagnoses([]); }
      
      try {
        setSelectedSymptoms(JSON.parse(record.symptoms || '[]'));
      } catch { setSelectedSymptoms([]); }
      
      setTreatment(record.treatment || '');
      
      try {
        setSelectedPrescriptions(JSON.parse(record.prescription || '[]'));
      } catch { setSelectedPrescriptions([]); }
      
      setNotes(record.notes || '');
      setOtherFee(record.otherFee || 0);

      // Fetch prescription items from database
      try {
        const itemsResponse = await api.get(`/MedicalRecords/${params.id}/prescription-items`);
        const dbItems = itemsResponse.data;
        setPrescriptionItems(dbItems);
        
        console.log('DB Items:', dbItems);
        
        // ALWAYS use database items as source of truth if they exist
        const mergedPrescriptions: PrescriptionItem[] = dbItems.map((dbItem: any) => ({
          id: dbItem.id,
          type: dbItem.itemType === 'Medicine' ? 'drug' : 'test',
          code: dbItem.itemCode,
          name: dbItem.itemName,
          quantity: dbItem.quantity,
          unit: dbItem.unit,
          fee: dbItem.price,
          status: dbItem.status,
          cancelReason: dbItem.cancelReason,
          createdAt: dbItem.createdAt,
          updatedAt: dbItem.updatedAt,
        }));
        
        setSelectedPrescriptions(mergedPrescriptions);
        console.log('Merged prescription items:', mergedPrescriptions);
      } catch (error) {
        console.error('Error fetching prescription items:', error);
        // Fallback to JSON only if DB fetch completely fails
        try {
          const jsonItems = JSON.parse(record.prescription || '[]');
          setSelectedPrescriptions(jsonItems);
        } catch {
          setSelectedPrescriptions([]);
        }
      }

      // Fetch reference data
      const [diagnosesRes, symptomsRes, drugsRes, testsRes] = await Promise.all([
        api.get('/MedicalRecords/reference-data/diagnoses'),
        api.get('/MedicalRecords/reference-data/symptoms'),
        api.get('/MedicalRecords/reference-data/drugs'),
        api.get('/MedicalRecords/reference-data/medical-tests'),
      ]);

      setDiagnoses(diagnosesRes.data);
      setSymptoms(symptomsRes.data);
      setDrugs(drugsRes.data);
      setTests(testsRes.data);
    } catch (error: any) {
      console.error('Error fetching data:', error);
      toast.error(error.response?.data?.message || 'Không thể tải dữ liệu!');
    } finally {
      setIsLoading(false);
    }
  };

  const calculateFees = () => {
    let medicineFee = 0;
    let testFee = 0;

    selectedPrescriptions.forEach((item) => {
      if (item.type === 'drug' && item.status !== 'Cancelled') {
        medicineFee += (item.fee || 0) * (item.quantity || 1);
      } else if (item.type === 'test' && item.status !== 'Cancelled') {
        testFee += item.fee || 0;
      }
    });

    return { medicineFee, testFee };
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const { medicineFee, testFee } = calculateFees();

      await api.put(`/MedicalRecords/${params.id}`, {
        diagnosis: JSON.stringify(selectedDiagnoses),
        symptoms: JSON.stringify(selectedSymptoms),
        treatment,
        prescription: JSON.stringify(selectedPrescriptions),
        notes,
        medicineFee,
        testFee,
        otherFee,
      });

      toast.success('Lưu thành công!');
      await fetchData(); // Refresh data
    } catch (error: any) {
      console.error('Error saving:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi lưu!');
    } finally {
      setIsSaving(false);
    }
  };

  const handleComplete = async () => {
    if (!confirm('Xác nhận hoàn thành khám bệnh? Bệnh nhân sẽ được yêu cầu thanh toán viện phí.')) return;

    try {
      await handleSave(); // Save first
      await api.post(`/MedicalRecords/${params.id}/complete`);
      toast.success('Hoàn thành khám bệnh!');
      router.push('/doctor/portal?section=appointments');
    } catch (error: any) {
      console.error('Error completing:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi hoàn thành!');
    }
  };

  const handleHospitalize = async () => {
    if (!confirm('Xác nhận chuyển nhập viện? Hồ sơ sẽ được lưu và có thể tiếp tục điều trị sau.')) return;

    try {
      await handleSave(); // Save first
      await api.post(`/MedicalRecords/${params.id}/hospitalize`);
      toast.success('Đã chuyển nhập viện!');
      router.push('/doctor/portal?section=appointments');
    } catch (error: any) {
      console.error('Error hospitalizing:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi nhập viện!');
    }
  };

  // Check if record is read-only based on appointment status
  useEffect(() => {
    if (medicalRecord?.appointment?.status === 'Completed') {
      setIsReadOnly(true);
      toast.success('Hồ sơ đã hoàn thành - Chỉ xem', { icon: '📋' });
    } else {
      setIsReadOnly(false);
    }
  }, [medicalRecord]);

  const fetchHistory = async () => {
    try {
      const response = await api.get(`/MedicalRecords/${params.id}/history`);
      setHistoryItems(response.data);
      setShowHistoryModal(true);
    } catch (error: any) {
      console.error('Error fetching history:', error);
      toast.error('Không thể tải lịch sử!');
    }
  };

  const handleCancelPrescriptionItem = async (itemId: number, status: string) => {
    if (status === 'Pending') {
      // Direct delete if Pending
      if (!confirm('Xác nhận xóa mục này?')) return;
      
      try {
        await api.post(`/MedicalRecords/prescription-items/${itemId}/request-cancel`, { reason: 'Deleted by doctor' });
        toast.success('Đã xóa!');
        await fetchData(); // Refresh
      } catch (error: any) {
        console.error('Error deleting item:', error);
        toast.error('Lỗi khi xóa!');
      }
    } else {
      // Request cancel if already Confirmed
      setCancelItemId(itemId);
      setShowCancelModal(true);
    }
  };

  const submitCancelRequest = async () => {
    if (!cancelItemId || !cancelReason.trim()) {
      toast.error('Vui lòng nhập lý do hủy!');
      return;
    }

    try {
      await api.post(`/MedicalRecords/prescription-items/${cancelItemId}/request-cancel`, {
        reason: cancelReason
      });
      toast.success('Đã gửi yêu cầu hủy!');
      setShowCancelModal(false);
      setCancelItemId(null);
      setCancelReason('');
      await fetchData(); // Refresh
    } catch (error: any) {
      console.error('Error requesting cancel:', error);
      toast.error('Lỗi khi gửi yêu cầu!');
    }
  };

  const handleCompleteTest = async (itemId: number) => {
    if (!confirm('Xác nhận hoàn thành xét nghiệm này?')) return;

    try {
      await api.post(`/MedicalRecords/prescription-items/${itemId}/complete`);
      toast.success('Đã hoàn thành xét nghiệm!');
      await fetchData(); // Refresh data
    } catch (error: any) {
      console.error('Error completing test:', error);
      toast.error(error.response?.data?.message || 'Lỗi khi cập nhật trạng thái!');
    }
  };

  if (isLoading || !medicalRecord) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-emerald-50 via-teal-50 to-cyan-100">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-emerald-600"></div>
      </div>
    );
  }

  const { medicineFee, testFee } = calculateFees();
  const totalFee = medicalRecord.consultationFee + medicineFee + testFee + otherFee;

  return (
    <div className="min-h-screen bg-gradient-to-br from-emerald-50 via-teal-50 to-cyan-100 p-6">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className="bg-white rounded-2xl shadow-xl p-6 mb-6">
          <button
            onClick={() => router.push('/doctor/portal?section=appointments')}
            className="flex items-center space-x-2 text-gray-600 hover:text-emerald-600 mb-4 transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
            <span>Quay lại danh sách</span>
          </button>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {/* Patient Info */}
            <div className="flex items-center space-x-4">
              <div className="w-16 h-16 rounded-full bg-gradient-to-br from-emerald-400 to-teal-500 flex items-center justify-center text-white font-bold text-2xl shadow-lg">
                {medicalRecord.patient?.name?.[0] || 'P'}
              </div>
              <div>
                <p className="text-sm text-gray-500">Bệnh nhân</p>
                <p className="text-lg font-bold text-gray-900">{medicalRecord.patient?.name}</p>
                <p className="text-sm text-gray-600">{medicalRecord.patient?.age} tuổi - {medicalRecord.patient?.email}</p>
              </div>
            </div>

            {/* Appointment Info */}
            <div>
              <p className="text-sm text-gray-500">Ngày khám bệnh</p>
              <p className="text-lg font-bold text-gray-900">
                {new Date(medicalRecord.appointment?.date).toLocaleDateString('vi-VN')}
              </p>
              <p className="text-sm text-gray-600">
                {new Date(medicalRecord.appointment?.date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
              </p>
            </div>

            {/* Status */}
            <div>
              <p className="text-sm text-gray-500">Trạng thái</p>
              <span className={`inline-block px-4 py-2 rounded-full text-sm font-semibold mt-1 ${
                medicalRecord.appointment?.status === 'Completed' ? 'bg-green-100 text-green-700' :
                medicalRecord.appointment?.status === 'Hospitalized' ? 'bg-purple-100 text-purple-700' :
                'bg-blue-100 text-blue-700'
              }`}>
                {medicalRecord.appointment?.status === 'InProgress' ? 'Đang khám' :
                 medicalRecord.appointment?.status === 'Completed' ? 'Hoàn thành' :
                 medicalRecord.appointment?.status === 'Hospitalized' ? 'Nhập viện' :
                 medicalRecord.appointment?.status}
              </span>
            </div>
          </div>
        </div>

        {/* Main Form */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left Column - Medical Information */}
          <div className="lg:col-span-2 space-y-6">
            {/* Diagnosis Section */}
            <div className="bg-white rounded-2xl shadow-xl p-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
                <FileText className="w-5 h-5 text-emerald-600" />
                <span>Chẩn đoán (ICD-10)</span>
              </h3>
              
              <div className="relative mb-4">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="text"
                  placeholder="Tìm kiếm chẩn đoán (mã hoặc tên)..."
                  value={diagnosisSearch}
                  onChange={(e) => setDiagnosisSearch(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  disabled={isReadOnly}
                />
              </div>

              <div className="max-h-60 overflow-y-auto space-y-2 mb-4 border border-gray-200 rounded-lg p-2">
                {diagnoses
                  .filter((d) => 
                    d.name?.toLowerCase().includes(diagnosisSearch.toLowerCase()) ||
                    d.code?.toLowerCase().includes(diagnosisSearch.toLowerCase())
                  )
                  .slice(0, 10)
                  .map((diagnosis) => (
                    <button
                      key={diagnosis.code}
                      onClick={() => {
                        if (!selectedDiagnoses.find((d) => d.code === diagnosis.code)) {
                          const newDiagnoses = [...selectedDiagnoses, diagnosis];
                          setSelectedDiagnoses(newDiagnoses);
                          console.log('Added diagnosis:', diagnosis.name);
                          console.log('Total diagnoses:', newDiagnoses.length);
                          toast.success(`Đã thêm: ${diagnosis.name}`);
                        } else {
                          toast.error('Chẩn đoán này đã được chọn!');
                        }
                      }}
                      className="w-full text-left p-3 border border-gray-200 rounded-lg hover:bg-emerald-50 transition-colors"
                    >
                      <p className="font-semibold text-sm text-emerald-600">{diagnosis.code}</p>
                      <p className="text-sm font-medium">{diagnosis.name}</p>
                      <p className="text-xs text-gray-600 mt-1">{diagnosis.description}</p>
                    </button>
                  ))}
              </div>

              <div className="space-y-2">
                <p className="text-sm font-semibold text-gray-700">Đã chọn ({selectedDiagnoses.length}):</p>
                {selectedDiagnoses.length === 0 ? (
                  <p className="text-sm text-gray-500 italic">Chưa có chẩn đoán nào</p>
                ) : (
                  selectedDiagnoses.map((diagnosis, index) => (
                    <div key={index} className="flex items-start justify-between p-3 bg-emerald-50 border border-emerald-200 rounded-lg">
                      <div className="flex-1">
                        <p className="text-sm font-bold text-emerald-700">{diagnosis.code}</p>
                        <p className="text-sm font-semibold">{diagnosis.name}</p>
                      </div>
                      {!isReadOnly && (
                        <button
                          onClick={() => setSelectedDiagnoses(selectedDiagnoses.filter((_, i) => i !== index))}
                          className="text-red-600 hover:text-red-700 ml-2"
                        >
                          <X className="w-5 h-5" />
                        </button>
                      )}
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Symptoms Section */}
            <div className="bg-white rounded-2xl shadow-xl p-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4">Triệu chứng</h3>
              
              <div className="relative mb-4">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="text"
                  placeholder="Tìm kiếm triệu chứng..."
                  value={symptomSearch}
                  onChange={(e) => setSymptomSearch(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  disabled={isReadOnly}
                />
              </div>

              <div className="max-h-60 overflow-y-auto space-y-2 mb-4 border border-gray-200 rounded-lg p-2">
                {symptoms
                  .filter((s) => s.name?.toLowerCase().includes(symptomSearch.toLowerCase()))
                  .slice(0, 10)
                  .map((symptom, index) => (
                    <button
                      key={index}
                      onClick={() => {
                        if (!selectedSymptoms.find((s) => s.name === symptom.name)) {
                          setSelectedSymptoms([...selectedSymptoms, symptom]);
                        }
                      }}
                      className="w-full text-left p-3 border border-gray-200 rounded-lg hover:bg-emerald-50 transition-colors"
                    >
                      <p className="font-semibold text-sm">{symptom.name}</p>
                      <p className="text-xs text-gray-600">{symptom.description}</p>
                    </button>
                  ))}
              </div>

              <div className="space-y-2">
                <p className="text-sm font-semibold text-gray-700">Đã chọn ({selectedSymptoms.length}):</p>
                {selectedSymptoms.length === 0 ? (
                  <p className="text-sm text-gray-500 italic">Chưa có triệu chứng nào</p>
                ) : (
                  selectedSymptoms.map((symptom, index) => (
                    <div key={index} className="flex items-center justify-between p-3 bg-emerald-50 border border-emerald-200 rounded-lg">
                      <span className="text-sm font-medium">{symptom.name}</span>
                      {!isReadOnly && (
                        <button
                          onClick={() => setSelectedSymptoms(selectedSymptoms.filter((_, i) => i !== index))}
                          className="text-red-600 hover:text-red-700"
                        >
                          <X className="w-5 h-5" />
                        </button>
                      )}
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Treatment Section */}
            <div className="bg-white rounded-2xl shadow-xl p-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4">Phương pháp điều trị</h3>
              <textarea
                value={treatment}
                onChange={(e) => setTreatment(e.target.value)}
                rows={6}
                placeholder="Nhập phương pháp điều trị, chế độ chăm sóc, lưu ý..."
                className="w-full p-4 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
                disabled={isReadOnly}
              />
            </div>

            {/* Prescription Section */}
            <div className="bg-white rounded-2xl shadow-xl p-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
                <Pill className="w-5 h-5 text-emerald-600" />
                <span>Đơn thuốc & Xét nghiệm</span>
              </h3>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                {/* Drugs */}
                <div>
                  <p className="text-sm font-semibold text-gray-700 mb-2 flex items-center space-x-1">
                    <Pill className="w-4 h-4" />
                    <span>Thuốc</span>
                  </p>
                  <div className="relative mb-2">
                    <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="text"
                      placeholder="Tìm thuốc..."
                      value={drugSearch}
                      onChange={(e) => setDrugSearch(e.target.value)}
                      className="w-full pl-9 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      disabled={isReadOnly}
                    />
                  </div>
                  <div className="max-h-40 overflow-y-auto space-y-1 border border-gray-200 rounded-lg p-2">
                    {drugs
                      .filter((d) => d.name?.toLowerCase().includes(drugSearch.toLowerCase()))
                      .slice(0, 5)
                      .map((drug, index) => (
                        <button
                          key={index}
                          onClick={() => {
                            const existing = selectedPrescriptions.find(
                              (p) => p.name === drug.name && p.type === "drug" && p.status?.toLowerCase() !== "cancelled" && p.status?.toLowerCase() !== "confirmed"
                            );
                            if (existing) {
                              toast.error("Thuốc này đã được kê và chưa bị hủy/hoàn thành!");
                            } else {
                              setSelectedPrescriptions([
                                ...selectedPrescriptions,
                                {
                                  type: "drug",
                                  code: drug.name,
                                  name: drug.name,
                                  dosage: drug.dosage,
                                  fee: drug.fee,
                                  unit: drug.unit,
                                  quantity: 1,
                                  status: "pending",
                                },
                              ]);
                              toast.success(`Đã thêm: ${drug.name}`);
                            }
                          }}
                          className="w-full text-left p-2 text-sm border border-gray-200 rounded-lg hover:bg-emerald-50 transition-colors"
                        >
                          <p className="font-semibold">{drug.name}</p>
                          <p className="text-xs text-gray-600">{drug.dosage}</p>
                          <p className="text-xs text-emerald-600 font-semibold">{drug.fee?.toLocaleString()}đ/{drug.unit}</p>
                        </button>
                      ))}
                  </div>
                </div>

                {/* Tests */}
                <div>
                  <p className="text-sm font-semibold text-gray-700 mb-2 flex items-center space-x-1">
                    <TestTube className="w-4 h-4" />
                    <span>Xét nghiệm</span>
                  </p>
                  <div className="relative mb-2">
                    <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="text"
                      placeholder="Tìm xét nghiệm..."
                      value={testSearch}
                      onChange={(e) => setTestSearch(e.target.value)}
                      className="w-full pl-9 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      disabled={isReadOnly}
                    />
                  </div>
                  <div className="max-h-40 overflow-y-auto space-y-1 border border-gray-200 rounded-lg p-2">
                    {tests
                      .filter((t) => t.testName?.toLowerCase().includes(testSearch.toLowerCase()))
                      .slice(0, 5)
                      .map((test, index) => (
                        <button
                          key={index}
                          onClick={() => {
                            const existing = selectedPrescriptions.find(
                              (p) => p.name === test.testName && p.type === "test" && 
                              p.status?.toLowerCase() !== "cancelled" && p.status?.toLowerCase() !== "completed"
                            );
                            if (existing) {
                              toast.error("Xét nghiệm này đã được chỉ định và chưa hoàn tất!");
                            } else {
                              setSelectedPrescriptions([
                                ...selectedPrescriptions,
                                {
                                  type: "test",
                                  code: test.testName,
                                  name: test.testName,
                                  testName: test.testName,
                                  description: test.subcategory,
                                  subcategory: test.subcategory,
                                  fee: test.fee,
                                  unit: "lần",
                                  status: "pending",
                                },
                              ]);
                              toast.success(`Đã thêm: ${test.testName}`);
                            }
                          }}
                          className="w-full text-left p-2 text-sm border border-gray-200 rounded-lg hover:bg-emerald-50 transition-colors"
                        >
                          <p className="font-semibold">{test.testName}</p>
                          <p className="text-xs text-gray-600">{test.subcategory}</p>
                          <p className="text-xs text-blue-600 font-semibold">{test.fee?.toLocaleString()}đ</p>
                        </button>
                      ))}
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <p className="text-sm font-semibold text-gray-700">Đơn thuốc & XN ({selectedPrescriptions.length} mục):</p>
                {selectedPrescriptions.length === 0 ? (
                  <p className="text-sm text-gray-500 italic">Chưa có đơn thuốc hoặc xét nghiệm</p>
                ) : (
                  selectedPrescriptions.map((item, index) => (
                    <div key={index} className={`flex items-center justify-between p-3 rounded-lg border-2 ${
                      item.type === 'drug' ? 'bg-pink-50 border-pink-200' : 'bg-blue-50 border-blue-200'
                    }`}>
                      <div className="flex-1">
                        <div className="flex items-start justify-between">
                          <div className="flex items-center space-x-2">
                            {item.type === 'drug' ? <Pill className="w-4 h-4 text-pink-600" /> : <TestTube className="w-4 h-4 text-blue-600" />}
                            <p className="text-sm font-semibold">{item.name}</p>
                            
                            {/* Show status badge */}
                            {item.status && item.status !== 'Pending' && (
                              <span className={`text-xs px-2 py-0.5 rounded-full ${
                                item.status === 'Confirmed' ? 'bg-green-100 text-green-700' :
                                item.status === 'CancelRequested' ? 'bg-orange-100 text-orange-700' :
                                'bg-gray-100 text-gray-700'
                              }`}>
                                {item.status === 'Confirmed' ? 'Đã xác nhận' :
                                 item.status === 'CancelRequested' ? 'Chờ duyệt hủy' :
                                 item.status}
                              </span>
                            )}
                          </div>
                          <span className="text-xs text-gray-500 whitespace-nowrap ml-2">
                            {formatDateTimeToVN(item.updatedAt || item.createdAt)}
                          </span>
                        </div>
                        {item.type === 'drug' && (
                          <div className="flex items-center space-x-2 mt-2">
                            <label className="text-xs text-gray-600">Số lượng:</label>
                            <input
                              type="number"
                              min="1"
                              value={item.quantity}
                              onChange={(e) => {
                                const newPrescriptions = [...selectedPrescriptions];
                                newPrescriptions[index].quantity = parseInt(e.target.value) || 1;
                                setSelectedPrescriptions(newPrescriptions);
                              }}
                              className="w-20 px-2 py-1 text-sm border border-gray-300 rounded"
                              disabled={isReadOnly || !!(item.status && item.status !== 'Pending')}
                            />
                            <span className="text-xs text-gray-600">{item.unit}</span>
                          </div>
                        )}
                        <p className={`text-xs font-semibold mt-1 ${
                          item.type === 'drug' ? 'text-pink-600' : 'text-blue-600'
                        }`}>
                          {item.type === 'drug' 
                            ? `${((item.fee || 0) * (item.quantity || 1)).toLocaleString()}đ`
                            : `${(item.fee || 0).toLocaleString()}đ`}
                        </p>
                        
                        {/* Show cancel reason if requested */}
                        {item.status === 'CancelRequested' && item.cancelReason && (
                          <p className="text-xs text-orange-600 mt-1 italic">
                            Lý do hủy: {item.cancelReason}
                          </p>
                        )}
                      </div>
                      <div className="flex items-center space-x-2">
                        {item.type === 'test' && item.status === 'Confirmed' && !isReadOnly && (
                          <button
                            onClick={() => handleCompleteTest(item.id!)}
                            className="text-teal-600 hover:text-teal-700"
                            title="Đánh dấu hoàn thành"
                          >
                            <CheckCircle className="w-5 h-5" />
                          </button>
                        )}
                        {!isReadOnly && (
                          <button
                            onClick={() => {
                              if (item.id && item.status) {
                                // Item from database with status
                                handleCancelPrescriptionItem(item.id, item.status);
                              } else {
                                // New item not yet saved - can delete directly
                                setSelectedPrescriptions(selectedPrescriptions.filter((_, i) => i !== index));
                              }
                            }}
                            disabled={item.status === 'CancelRequested' || item.status === 'Cancelled' || item.status === 'Completed'}
                            className={`ml-2 ${
                              item.status === 'CancelRequested' || item.status === 'Cancelled' || item.status === 'Completed'
                                ? 'text-gray-400 cursor-not-allowed'
                                : 'text-red-600 hover:text-red-700'
                            }`}
                            title={
                              item.status === 'Completed' ? 'Đã hoàn thành, không thể xóa' :
                              item.status === 'Cancelled' ? 'Đã bị hủy' :
                              item.status === 'CancelRequested' ? 'Đang chờ duyệt hủy' :
                              item.status === 'Confirmed' ? 'Yêu cầu hủy (đã xác nhận)' :
                              'Xóa'
                            }
                          >
                            <X className="w-5 h-5" />
                          </button>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Notes Section */}
            <div className="bg-white rounded-2xl shadow-xl p-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4">Ghi chú</h3>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={4}
                placeholder="Ghi chú thêm về tình trạng bệnh nhân, lời dặn..."
                className="w-full p-4 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
                disabled={isReadOnly}
              />
            </div>
          </div>

          {/* Right Column - Billing & Actions */}
          <div className="space-y-6">
            <div className="bg-white rounded-2xl shadow-xl p-6 sticky top-6">
              <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center space-x-2">
                <DollarSign className="w-5 h-5 text-emerald-600" />
                <span>Chi phí điều trị</span>
              </h3>

              <div className="space-y-3">
                <div className="flex justify-between items-center pb-2 border-b">
                  <span className="text-gray-600">Phí khám:</span>
                  <span className="font-semibold">{medicalRecord.consultationFee.toLocaleString()}đ</span>
                </div>
                <div className="flex justify-between items-center pb-2 border-b">
                  <span className="text-gray-600">Phí thuốc:</span>
                  <span className="font-semibold text-pink-600">{medicineFee.toLocaleString()}đ</span>
                </div>
                <div className="flex justify-between items-center pb-2 border-b">
                  <span className="text-gray-600">Phí xét nghiệm:</span>
                  <span className="font-semibold text-blue-600">{testFee.toLocaleString()}đ</span>
                </div>
                <div className="flex justify-between items-center pb-2 border-b">
                  <span className="text-gray-600">Phí khác:</span>
                  <input
                    type="number"
                    value={otherFee}
                    onChange={(e) => setOtherFee(parseFloat(e.target.value) || 0)}
                    className="w-32 text-right px-2 py-1 border border-gray-300 rounded font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    disabled={isReadOnly}
                  />
                </div>
                <div className="flex justify-between items-center pt-2 border-t-2 border-gray-800">
                  <span className="text-lg font-bold">Tổng cộng:</span>
                  <span className="text-xl font-bold text-emerald-600">{totalFee.toLocaleString()}đ</span>
                </div>
                <div className="flex justify-between items-center text-sm pt-2 border-t">
                  <span className="text-gray-600">Đã thanh toán:</span>
                  <span className="font-semibold text-green-600">{medicalRecord.paidAmount.toLocaleString()}đ</span>
                </div>
                <div className="flex justify-between items-center text-sm pb-3 border-b">
                  <span className="text-gray-600">Còn lại:</span>
                  <span className="font-semibold text-orange-600">{(totalFee - medicalRecord.paidAmount).toLocaleString()}đ</span>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="space-y-3 mt-6">
                <button
                  onClick={fetchHistory}
                  className="w-full px-4 py-3 bg-indigo-600 text-white rounded-xl hover:bg-indigo-700 transition-colors font-semibold flex items-center justify-center space-x-2 shadow-lg"
                >
                  <FileText className="w-5 h-5" />
                  <span>Xem lịch sử</span>
                </button>

                {!isReadOnly ? (
                  <>
                    <button
                      onClick={handleSave}
                      disabled={isSaving}
                      className="w-full px-4 py-3 bg-gray-600 text-white rounded-xl hover:bg-gray-700 transition-colors font-semibold flex items-center justify-center space-x-2 disabled:opacity-50 disabled:cursor-not-allowed shadow-lg"
                    >
                      <Save className="w-5 h-5" />
                      <span>{isSaving ? 'Đang lưu...' : 'Lưu nháp'}</span>
                    </button>

                    <button
                      onClick={handleComplete}
                      disabled={isSaving}
                      className="w-full px-4 py-3 bg-emerald-600 text-white rounded-xl hover:bg-emerald-700 transition-colors font-semibold flex items-center justify-center space-x-2 disabled:opacity-50 shadow-lg"
                    >
                      <CheckCircle className="w-5 h-5" />
                      <span>Hoàn thành khám</span>
                    </button>

                    {/* Only show Hospitalize button if not already hospitalized */}
                    {medicalRecord.appointment?.status !== 'Hospitalized' && (
                      <button
                        onClick={handleHospitalize}
                        disabled={isSaving}
                        className="w-full px-4 py-3 bg-purple-600 text-white rounded-xl hover:bg-purple-700 transition-colors font-semibold flex items-center justify-center space-x-2 disabled:opacity-50 shadow-lg"
                      >
                        <Building2 className="w-5 h-5" />
                        <span>Nhập viện</span>
                      </button>
                    )}
                  </>
                ) : (
                  <div className="bg-green-50 border-2 border-green-200 px-4 py-4 rounded-xl">
                    <p className="text-green-700 font-semibold text-center flex items-center justify-center space-x-2">
                      <CheckCircle className="w-5 h-5" />
                      <span>Hồ sơ đã hoàn thành - Chỉ xem</span>
                    </p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

      {/* History Modal */}
      {showHistoryModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-4xl w-full max-h-[90vh] overflow-y-auto">
            <div className="sticky top-0 bg-gradient-to-r from-indigo-500 to-purple-500 text-white p-6 rounded-t-2xl flex items-center justify-between">
              <h3 className="text-2xl font-bold">Lịch sử thay đổi</h3>
              <button
                onClick={() => setShowHistoryModal(false)}
                className="p-2 hover:bg-white/20 rounded-lg transition-colors"
              >
                <X className="w-6 h-6" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              {historyItems.length === 0 ? (
                <p className="text-gray-500 text-center py-8">Chưa có lịch sử thay đổi</p>
              ) : (
                historyItems.map((item) => (
                  <div key={item.id} className="border border-gray-200 rounded-xl p-4 hover:border-indigo-300 transition-colors">
                    <div className="flex items-center justify-between mb-2">
                      <span className={`px-3 py-1 rounded-full text-xs font-semibold ${
                        item.action === 'Create' ? 'bg-green-100 text-green-700' :
                        item.action === 'Update' ? 'bg-blue-100 text-blue-700' :
                        item.action === 'Complete' ? 'bg-emerald-100 text-emerald-700' :
                        'bg-purple-100 text-purple-700'
                      }`}>
                        {item.action === 'Create' ? 'Tạo mới' :
                         item.action === 'Update' ? 'Cập nhật' :
                         item.action === 'Complete' ? 'Hoàn thành' :
                         'Nhập viện'}
                      </span>
                      <span className="text-sm text-gray-500">
                        {new Date(item.createdAt).toLocaleString('vi-VN')}
                      </span>
                    </div>
                    <div className="grid grid-cols-3 gap-2 mt-2 text-sm">
                      <div className="bg-gray-50 p-2 rounded">
                        <p className="text-xs text-gray-500">Phí thuốc</p>
                        <p className="font-semibold">{item.medicineFee.toLocaleString()}đ</p>
                      </div>
                      <div className="bg-gray-50 p-2 rounded">
                        <p className="text-xs text-gray-500">Phí XN</p>
                        <p className="font-semibold">{item.testFee.toLocaleString()}đ</p>
                      </div>
                      <div className="bg-gray-50 p-2 rounded">
                        <p className="text-xs text-gray-500">Phí khác</p>
                        <p className="font-semibold">{item.otherFee.toLocaleString()}đ</p>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {/* Cancel Modal */}
      {showCancelModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-md w-full">
            <div className="bg-gradient-to-r from-red-500 to-orange-500 text-white p-6 rounded-t-2xl">
              <h3 className="text-xl font-bold">Yêu cầu hủy</h3>
            </div>
            <div className="p-6">
              <p className="text-gray-600 mb-4">Vui lòng nhập lý do hủy mục này:</p>
              <textarea
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                rows={4}
                placeholder="Ví dụ: Bệnh nhân không đủ điều kiện xét nghiệm..."
                className="w-full p-3 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-red-500 resize-none"
              />
            </div>
            <div className="bg-gray-50 p-6 rounded-b-2xl flex justify-end space-x-3">
              <button
                onClick={() => {
                  setShowCancelModal(false);
                  setCancelItemId(null);
                  setCancelReason('');
                }}
                className="px-6 py-2 bg-gray-200 text-gray-700 rounded-xl hover:bg-gray-300 transition-colors font-semibold"
              >
                Hủy
              </button>
              <button
                onClick={submitCancelRequest}
                className="px-6 py-2 bg-red-600 text-white rounded-xl hover:bg-red-700 transition-colors font-semibold"
              >
                Gửi yêu cầu
              </button>
            </div>
          </div>
        </div>
      )}
      </div>
    </div>
  );
}
