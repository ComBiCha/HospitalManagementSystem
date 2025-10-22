import React, { useState } from 'react';

interface ExternalPatientHistoryDto {
    medicationRequests: string;
    medicationStatements: string;
    allergyIntolerances: string;
    conditions: string;
    observations: string;
}

interface Props {
    onFetchHistory: (resources: string[]) => void;
    history: ExternalPatientHistoryDto | null;
    isLoading: boolean;
}

const translations = {
    status: "Trạng thái",
    requester: "Người yêu cầu",
    source: "Nguồn",
    clinicalStatus: "Tình trạng lâm sàng",
    verification: "Xác minh",
    onset: "Khởi phát",
    value: "Giá trị",
    effective: "Hiệu lực",
    unnamedMedicationRequest: "Yêu cầu thuốc không tên",
    unnamedMedicationStatement: "Tuyên bố thuốc không tên",
    unknownAllergy: "Dị ứng không xác định",
    unknownCondition: "Tình trạng không xác định",
    unknownObservation: "Quan sát không xác định",
    unknownResource: "Tài nguyên không xác định",
    noResourceData: "Không có dữ liệu tài nguyên.",
    noRecordsFound: (title: string) => `Không tìm thấy ${title.toLowerCase()} nào trong hồ sơ bên ngoài.`,
    couldNotParse: (title: string) => `Không thể phân tích ${title.toLowerCase()}.`,
    externalClinicalHistory: "Lịch sử lâm sàng bên ngoài (Epic)",
    medicationRequests: "Yêu cầu thuốc",
    medicationStatements: "Tuyên bố thuốc",
    allergies: "Dị ứng",
    conditions: "Tình trạng",
    labResults: "Kết quả xét nghiệm",
    fetchHistory: "Lấy lịch sử",
    fetchingHistory: "Đang lấy lịch sử...",
    selectResources: "Chọn loại thông tin muốn lấy:",
};

const resourceTypes = [
    { id: 'MedicationRequest', label: translations.medicationRequests },
    { id: 'MedicationStatement', label: translations.medicationStatements },
    { id: 'AllergyIntolerance', label: translations.allergies },
    { id: 'Condition', label: translations.conditions },
    { id: 'Observation', label: translations.labResults },
];

const renderResource = (resource: any) => {
    if (!resource) return <p>{translations.noResourceData}</p>;

    if (resource.text && resource.text.div) {
        return <div dangerouslySetInnerHTML={{ __html: resource.text.div }} />;
    }

    let title = translations.unknownResource;
    let details = [];

    switch (resource.resourceType) {
        case 'MedicationRequest':
            title = resource.medicationCodeableConcept?.text || translations.unnamedMedicationRequest;
            details.push(`${translations.status}: ${resource.status}`);
            if(resource.requester?.display) details.push(`${translations.requester}: ${resource.requester.display}`);
            break;
        case 'MedicationStatement':
            title = resource.medicationCodeableConcept?.text || translations.unnamedMedicationStatement;
            details.push(`${translations.status}: ${resource.status}`);
            if(resource.informationSource?.display) details.push(`${translations.source}: ${resource.informationSource.display}`);
            break;
        case 'AllergyIntolerance':
            title = resource.code?.text || translations.unknownAllergy;
            details.push(`${translations.clinicalStatus}: ${resource.clinicalStatus?.coding?.[0]?.code}`);
            details.push(`${translations.verification}: ${resource.verificationStatus?.coding?.[0]?.code}`);
            break;
        case 'Condition':
            title = resource.code?.text || translations.unknownCondition;
            details.push(`${translations.clinicalStatus}: ${resource.clinicalStatus?.coding?.[0]?.code}`);
            if(resource.onsetDateTime) details.push(`${translations.onset}: ${new Date(resource.onsetDateTime).toLocaleDateString()}`);
            break;
        case 'Observation':
            title = resource.code?.text || translations.unknownObservation;
            if (resource.valueQuantity) {
                details.push(`${translations.value}: ${resource.valueQuantity.value} ${resource.valueQuantity.unit}`);
            }
            if (resource.effectiveDateTime) {
                details.push(`${translations.effective}: ${new Date(resource.effectiveDateTime).toLocaleDateString()}`);
            }
            break;
        default:
            break;
    }

    return (
        <div>
            <p className="font-semibold">{title}</p>
            <div className="text-xs text-gray-600 space-y-1 mt-1">
                {details.map((detail, i) => <p key={i}>{detail}</p>)}
            </div>
        </div>
    );
};

const ExternalHistoryPanel: React.FC<Props> = ({ onFetchHistory, history, isLoading }) => {
    const [selectedResources, setSelectedResources] = useState<string[]>([]);

    const handleCheckboxChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        const { value, checked } = event.target;
        setSelectedResources(prev =>
            checked ? [...prev, value] : prev.filter(item => item !== value)
        );
    };

    const handleFetchClick = () => {
        onFetchHistory(selectedResources);
    };

    const renderBundle = (title: string, bundleString: string) => {
        try {
            if (!bundleString) return null;
            const bundle = JSON.parse(bundleString);
            if (!bundle.entry || bundle.entry.length === 0) {
                return (
                    <div key={title}>
                        <h4 className="text-md font-bold text-gray-700 mb-2 border-b pb-2">{title}</h4>
                        <p className="text-sm text-gray-500 italic">{translations.noRecordsFound(title)}</p>
                    </div>
                );
            }
            return (
                <div key={title}>
                    <h4 className="text-md font-bold text-gray-700 mb-2 border-b pb-2">{title}</h4>
                    <ul className="space-y-3">
                        {bundle.entry.map((entry: any, index: number) => (
                            <li key={index} className="bg-gray-50 p-3 rounded-lg border border-gray-200">
                                {renderResource(entry.resource)}
                            </li>
                        ))}
                    </ul>
                </div>
            );
        } catch (e) {
            console.error(`Error parsing ${title}:`, e);
            return <p key={title}>{translations.couldNotParse(title)}</p>;
        }
    };

    return (
        <div className="bg-white rounded-2xl shadow-xl p-6">
            <h3 className="text-xl font-bold text-gray-900 mb-4">{translations.externalClinicalHistory}</h3>
            
            <div className="mb-4">
                <p className="font-semibold mb-2">{translations.selectResources}</p>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
                    {resourceTypes.map(resource => (
                        <label key={resource.id} className="flex items-center space-x-2">
                            <input
                                type="checkbox"
                                value={resource.id}
                                onChange={handleCheckboxChange}
                                className="form-checkbox h-5 w-5 text-blue-600"
                            />
                            <span>{resource.label}</span>
                        </label>
                    ))}
                </div>
            </div>

            <button
                onClick={handleFetchClick}
                disabled={isLoading || selectedResources.length === 0}
                className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded disabled:bg-gray-400"
            >
                {isLoading ? translations.fetchingHistory : translations.fetchHistory}
            </button>

            <div className="space-y-6 mt-6">
                {history && (
                    <>
                        {renderBundle(translations.medicationRequests, history.medicationRequests)}
                        {renderBundle(translations.medicationStatements, history.medicationStatements)}
                        {renderBundle(translations.allergies, history.allergyIntolerances)}
                        {renderBundle(translations.conditions, history.conditions)}
                        {renderBundle(translations.labResults, history.observations)}
                    </>
                )}
            </div>
        </div>
    );
};

export default ExternalHistoryPanel;