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
}

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
      if (item.type === 'drug') {
        medicineFee += (item.fee || 0) * (item.quantity || 1);
      } else if (item.type === 'test') {
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
                            if (!selectedPrescriptions.find((p) => p.name === drug.name && p.type === 'drug')) {
                              setSelectedPrescriptions([...selectedPrescriptions, { ...drug, type: 'drug', quantity: 1 }]);
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
                            if (!selectedPrescriptions.find((p) => p.testName === test.testName && p.type === 'test')) {
                              setSelectedPrescriptions([...selectedPrescriptions, { 
                                name: test.testName,
                                testName: test.testName,
                                description: test.subcategory,
                                subcategory: test.subcategory,
                                fee: test.fee,
                                type: 'test' 
                              }]);
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
                        <div className="flex items-center space-x-2">
                          {item.type === 'drug' ? <Pill className="w-4 h-4 text-pink-600" /> : <TestTube className="w-4 h-4 text-blue-600" />}
                          <p className="text-sm font-semibold">{item.name}</p>
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
                              disabled={isReadOnly}
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
                      </div>
                      {!isReadOnly && (
                        <button
                          onClick={() => setSelectedPrescriptions(selectedPrescriptions.filter((_, i) => i !== index))}
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
      </div>
    </div>
  );
}
