'use client';

interface MedicalRecordsSectionProps {
  patientId: number | null;
}

export default function MedicalRecordsSection({ patientId }: MedicalRecordsSectionProps) {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Hồ sơ bệnh án</h2>
        <p className="text-gray-600 mt-1">Xem lịch sử khám bệnh và hồ sơ y tế của bạn</p>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
        <div className="text-gray-400 mb-4">
          <svg className="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
        </div>
        <h3 className="text-lg font-medium text-gray-900 mb-2">Chưa có hồ sơ bệnh án</h3>
        <p className="text-gray-500">Hồ sơ bệnh án của bạn sẽ hiển thị tại đây sau khi khám bệnh.</p>
      </div>
    </div>
  );
}