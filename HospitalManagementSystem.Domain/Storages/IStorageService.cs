namespace HospitalManagementSystem.Domain.Storages
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream stream, string fileName, string contentType = "application/octet-stream");
        Task<bool> DeleteAsync(string fileId);
        Task<Stream?> DownloadAsync(string fileId);
    }
}