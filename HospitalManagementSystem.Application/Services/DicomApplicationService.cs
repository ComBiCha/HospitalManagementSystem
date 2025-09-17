using HospitalManagementSystem.Domain.Dicom;
using System.Threading.Tasks;

namespace HospitalManagementSystem.Application.Services
{
    public class DicomApplicationService
    {
        private readonly IDicomService _dicomService;

        public DicomApplicationService(IDicomService dicomService)
        {
            _dicomService = dicomService;
        }

        public async Task UploadDicomAsync(string dicomFilePath)
            => await _dicomService.UploadDicomAsync(dicomFilePath);

        public async Task<string> GetAllPatientsAsync()
            => await _dicomService.GetAllPatientsAsync();

        public async Task<string> GetPatientDetailsAsync(string patientId)
            => await _dicomService.GetPatientDetailsAsync(patientId);

        public async Task<string> GetStudyDetailsAsync(string studyId)
            => await _dicomService.GetStudyDetailsAsync(studyId);

        public async Task<string> GetSeriesDetailsAsync(string seriesId)
            => await _dicomService.GetSeriesDetailsAsync(seriesId);

        public async Task<string> GetInstanceDetailsAsync(string instanceId)
            => await _dicomService.GetInstanceDetailsAsync(instanceId);

        public async Task DownloadInstanceFileAsync(string instanceId, string savePath)
            => await _dicomService.DownloadInstanceFileAsync(instanceId, savePath);

        public async Task DownloadInstanceAsJpegAsync(string instanceId, string savePath)
            => await _dicomService.DownloadInstanceAsJpegAsync(instanceId, savePath);
    }
}