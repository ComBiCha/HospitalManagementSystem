using HospitalManagementSystem.Domain.Dicom;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HospitalManagementSystem.Infrastructure.Dicom
{
    public class DicomService : IDicomService
    {
        // Địa chỉ Orthanc nội bộ trong AKS
        private readonly string orthancBaseUrl = "http://orthanc-service:8042";
        private readonly string username = "orthanc";
        private readonly string password = "orthanc";
        private string patientURL => $"{orthancBaseUrl}/patients";
        private string studyURL => $"{orthancBaseUrl}/studies";
        private string seriesURL => $"{orthancBaseUrl}/series";
        private string instancesURL => $"{orthancBaseUrl}/instances";
        private string orthancUrl => $"{orthancBaseUrl}/instances";

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return client;
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