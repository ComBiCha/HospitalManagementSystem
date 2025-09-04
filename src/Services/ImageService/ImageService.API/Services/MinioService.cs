using ImageService.API.Models;
using Minio;
using Minio.DataModel.Args;

namespace ImageService.API.Services
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MinioService> _logger;
        private readonly string _bucketName;

        public MinioService(IMinioClient minioClient, IConfiguration configuration, ILogger<MinioService> logger)
        {
            _minioClient = minioClient;
            _configuration = configuration;
            _logger = logger;
            _bucketName = _configuration["MinIO:BucketName"] ?? "medical-images";
        }

        public async Task<string> UploadImageAsync(IFormFile file, string objectKey)
        {
            try
            {
                // Ensure bucket exists
                var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucketName);
                if (!await _minioClient.BucketExistsAsync(bucketExistsArgs))
                {
                    var makeBucketArgs = new MakeBucketArgs().WithBucket(_bucketName);
                    await _minioClient.MakeBucketAsync(makeBucketArgs);
                }

                using var stream = file.OpenReadStream();
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectKey)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType);

                await _minioClient.PutObjectAsync(putObjectArgs);
                
                _logger.LogInformation("Successfully uploaded image: {ObjectKey}", objectKey);
                return objectKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image: {ObjectKey}", objectKey);
                throw;
            }
        }

        public async Task<ImageDownloadResponse> DownloadImageAsync(string objectKey, string fileName, string contentType)
        {
            try
            {
                var memoryStream = new MemoryStream();
                
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectKey)
                    .WithCallbackStream(stream => stream.CopyTo(memoryStream));

                await _minioClient.GetObjectAsync(getObjectArgs);
                memoryStream.Position = 0;

                return new ImageDownloadResponse
                {
                    ImageStream = memoryStream,
                    ContentType = contentType,
                    FileName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading image: {ObjectKey}", objectKey);
                throw;
            }
        }

        public async Task<bool> DeleteImageAsync(string objectKey)
        {
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectKey);

                await _minioClient.RemoveObjectAsync(removeObjectArgs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image: {ObjectKey}", objectKey);
                return false;
            }
        }

        public async Task<bool> ImageExistsAsync(string objectKey)
        {
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectKey);

                await _minioClient.StatObjectAsync(statObjectArgs);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
