using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HospitalManagementSystem.Domain.Storages;

namespace HospitalManagementSystem.Infrastructure.Storage
{
    public class SeaweedAssignResult
    {
        public string Fid { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
    }

    public class SeaweedStorageService : IStorageService
    {
        private readonly HttpClient _http;
        private readonly ILogger<SeaweedStorageService> _logger;
        private readonly string _masterUrl;
        private readonly string _publicUrl;
        private readonly string _replication;

        public SeaweedStorageService(HttpClient http, IConfiguration config, ILogger<SeaweedStorageService> logger)
        {
            _http = http;
            _logger = logger;
            _masterUrl = config.GetValue<string>("SeaweedFS:MasterUrl")?.TrimEnd('/') ?? "http://seaweed-master:9333";
            _publicUrl = config.GetValue<string>("SeaweedFS:PublicUrl")?.TrimEnd('/') ?? "";
            _replication = config.GetValue<string>("SeaweedFS:Replication") ?? "001";
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType = "application/octet-stream")
        {
            // 1) assign
            var assignUri = $"{_masterUrl}/dir/assign?replication={_replication}";
            var assign = await _http.GetFromJsonAsync<SeaweedAssignResult>(assignUri);
            if (assign == null || string.IsNullOrEmpty(assign.Fid))
                throw new InvalidOperationException("SeaweedFS assign failed");

            // 2) upload to volume server
            var uploadHost = assign.Url; 
            var uploadUrl = $"http://{uploadHost}/{assign.Fid}";
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            var resp = await _http.PutAsync(uploadUrl, content);
            resp.EnsureSuccessStatusCode();

            // 3) return public URL (prefer assign.PublicUrl then configured PublicUrl)
            var publicBase = !string.IsNullOrEmpty(assign.PublicUrl) ? assign.PublicUrl : _publicUrl;
            if (string.IsNullOrEmpty(publicBase))
                return assign.Fid;

            return assign.Fid;
        }

        public async Task<string> UploadAnyFileAsync(Stream stream, string fileName, string contentType = "application/octet-stream")
        {
            // 1) assign
            var assignUri = $"{_masterUrl}/dir/assign?replication={_replication}";
            var assign = await _http.GetFromJsonAsync<SeaweedAssignResult>(assignUri);
            if (assign == null || string.IsNullOrEmpty(assign.Fid))
                throw new InvalidOperationException("SeaweedFS assign failed");

            // 2) upload to volume server
            var uploadHost = assign.Url;
            var uploadUrl = $"http://{uploadHost}/{assign.Fid}";
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            var resp = await _http.PutAsync(uploadUrl, content);
            resp.EnsureSuccessStatusCode();

            // 3) return public URL (prefer assign.PublicUrl then configured PublicUrl)
            var publicBase = !string.IsNullOrEmpty(assign.PublicUrl) ? assign.PublicUrl : _publicUrl;
            if (string.IsNullOrEmpty(publicBase))
                return assign.Fid;

            return assign.Fid;
        }
        public async Task<Stream?> DownloadAnyFileAsync(string fileId)
        {
            // fileId có thể là "4,02b1b7f36f" hoặc URL đầy đủ
            string url = fileId.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? fileId
                : $"http://seaweed-volume:8080/{fileId}";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStreamAsync();
        }

        public async Task<bool> DeleteAsync(string fileId)
        {
            if (string.IsNullOrEmpty(_publicUrl)) return false;
            var deleteUrl = $"{_publicUrl.TrimEnd('/')}/{fileId}";
            var resp = await _http.DeleteAsync(deleteUrl);
            return resp.IsSuccessStatusCode;
        }

        public async Task<Stream?> DownloadAsync(string fileId)
        {
            // fileId can be a full URL or just the FID. This handles both cases.
            string url = fileId.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? fileId
                : $"http://seaweed-volume:8080/{fileId}";

            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download file from SeaweedFS. URL: {Url}, Status: {StatusCode}", url, resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadAsStreamAsync();
        }

        public async Task<(Stream? Stream, string? ContentType)> GetFileWithContentTypeAsync(string fileId)
        {
            string url = fileId.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? fileId
                : $"http://seaweed-volume:8080/{fileId}";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download file from SeaweedFS. URL: {Url}, Status: {StatusCode}", url, resp.StatusCode);
                return (null, null);
            }

            var stream = await resp.Content.ReadAsStreamAsync();
            var contentType = resp.Content.Headers.ContentType?.ToString();
            return (stream, contentType);
        }
    }
}