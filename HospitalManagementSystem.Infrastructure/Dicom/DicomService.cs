using HospitalManagementSystem.Domain.Dicom;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using HospitalManagementSystem.Domain.Entities;
using Microsoft.AspNetCore.Http;
using HospitalManagementSystem.Domain.Storages;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace HospitalManagementSystem.Infrastructure.Dicom
{
    public class DicomService : IDicomService
    {
        private readonly HospitalDbContext _context;
        private readonly IStorageService _storageService;
        // Orthanc configuration
        private readonly string orthancBaseUrl = "http://orthanc-service:8042";
        private readonly string username = "orthanc";
        private readonly string password = "orthanc";
        private string patientURL => $"{orthancBaseUrl}/patients";
        private string studyURL => $"{orthancBaseUrl}/studies";
        private string seriesURL => $"{orthancBaseUrl}/series";
        private string instancesURL => $"{orthancBaseUrl}/instances";
        private string orthancUrl => $"{orthancBaseUrl}/instances";

        public DicomService(HospitalDbContext context, IStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return client;
        }

        public async Task<ImageInfo> UploadDicomImageAsync(int medicalRecordId, IFormFile file, string? description, string imageType)
        {
            var medicalRecord = await _context.MedicalRecords.FindAsync(medicalRecordId);
            if (medicalRecord == null)
            {
                throw new KeyNotFoundException("Medical record not found.");
            }

            // 1. Upload to SeaweedFS for persistent storage
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var fileId = await _storageService.UploadAsync(file.OpenReadStream(), fileName, file.ContentType);

            // 2. Upload to Orthanc to generate previews and for DICOM server capabilities
            string? orthancInstanceId = null;
            if (file.ContentType == "application/dicom" || imageType == "dicom")
            {
                using var client = CreateHttpClient();
                using var content = new StreamContent(file.OpenReadStream());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
                var response = await client.PostAsync(instancesURL, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseBody);
                    orthancInstanceId = jsonDoc.RootElement.GetProperty("ID").GetString();
                }
            }

            var imageInfo = new ImageInfo
            {
                MedicalRecordId = medicalRecordId,
                FileName = fileName,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Description = description,
                ImageType = imageType,
                UploadedAt = DateTime.UtcNow,
                MinioObjectKey = fileId, // This is the SeaweedFS file ID/path
                OrthancInstanceId = orthancInstanceId // Store the new Orthanc ID
            };

            _context.Images.Add(imageInfo);
            await _context.SaveChangesAsync();

            return imageInfo;
        }

        public async Task<ImageDownloadResponse> DownloadImageAsync(int imageId)
        {
            var imageInfo = await _context.Images.FindAsync(imageId);
            if (imageInfo == null)
            {
                throw new KeyNotFoundException("Image not found.");
            }

            var stream = await _storageService.DownloadAsync(imageInfo.MinioObjectKey);
            return new ImageDownloadResponse
            {
                ImageStream = stream,
                ContentType = imageInfo.ContentType,
                FileName = imageInfo.OriginalFileName
            };
        }

        public async Task<byte[]> GetImagePreviewAsync(int imageId)
        {
            var imageInfo = await _context.Images.FindAsync(imageId);
            if (imageInfo == null)
            {
                throw new KeyNotFoundException("Image not found.");
            }

            // If we have an Orthanc ID, use it to get the preview
            if (!string.IsNullOrEmpty(imageInfo.OrthancInstanceId))
            {
                using var client = CreateHttpClient();
                var response = await client.GetAsync($"{instancesURL}/{imageInfo.OrthancInstanceId}/preview");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }

            // Fallback or if it's not a DICOM file, return empty or a placeholder
            return Array.Empty<byte>();
        }

        public async Task<ImageInfo?> GetImageInfoAsync(int imageId)
        {
            return await _context.Images.FindAsync(imageId);
        }

        public async Task<IEnumerable<ImageInfo>> GetImagesForMedicalRecordAsync(int medicalRecordId)
        {
            return await _context.Images
                .Where(i => i.MedicalRecordId == medicalRecordId)
                .ToListAsync();
        }

        public async Task UploadDicomAsync(string dicomFilePath)
        {
            using var client = CreateHttpClient();
            using var content = new ByteArrayContent(File.ReadAllBytes(dicomFilePath));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
            var response = await client.PostAsync(orthancUrl, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<string> GetAllPatientsAsync()
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(patientURL);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetPatientDetailsAsync(string patientId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{patientURL}/{patientId}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetStudyDetailsAsync(string studyId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{studyURL}/{studyId}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetSeriesDetailsAsync(string seriesId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{seriesURL}/{seriesId}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetInstanceDetailsAsync(string instanceId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{instancesURL}/{instanceId}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task DownloadInstanceFileAsync(string instanceId, string savePath)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{instancesURL}/{instanceId}/file");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(savePath, bytes);
        }
        public async Task DownloadInstanceAsJpegAsync(string instanceId, string savePath)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync($"{instancesURL}/{instanceId}/preview");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(savePath, bytes);
        }
    }
}