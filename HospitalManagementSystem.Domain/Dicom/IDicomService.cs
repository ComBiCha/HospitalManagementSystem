using System.Threading.Tasks;

namespace HospitalManagementSystem.Domain.Dicom
{
    public interface IDicomService
    {
        Task UploadDicomAsync(string dicomFilePath);
        Task<string> GetAllPatientsAsync();
        Task<string> GetPatientDetailsAsync(string patientId);
        Task<string> GetStudyDetailsAsync(string studyId);
        Task<string> GetSeriesDetailsAsync(string seriesId);
        Task<string> GetInstanceDetailsAsync(string instanceId);
        Task DownloadInstanceFileAsync(string instanceId, string savePath);
        Task DownloadInstanceAsJpegAsync(string instanceId, string savePath);
    }
}