using HospitalManagementSystem.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HospitalManagementSystem.Domain.Dicom
{
    public interface IDicomService
    {
        Task<ImageInfo> UploadDicomImageAsync(int medicalRecordId, IFormFile file, string? description, string imageType);
        Task<ImageDownloadResponse> DownloadImageAsync(int imageId);
        Task<byte[]> GetImagePreviewAsync(int imageId);
        Task<ImageInfo?> GetImageInfoAsync(int imageId);
        Task<IEnumerable<ImageInfo>> GetImagesForMedicalRecordAsync(int medicalRecordId);
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