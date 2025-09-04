using ImageService.API.Models;

namespace ImageService.API.Services
{
    public interface IMinioService
    {
        Task<string> UploadImageAsync(IFormFile file, string objectKey);
        Task<ImageDownloadResponse> DownloadImageAsync(string objectKey, string fileName, string contentType);
        Task<bool> DeleteImageAsync(string objectKey);
        Task<bool> ImageExistsAsync(string objectKey);
    }
}
