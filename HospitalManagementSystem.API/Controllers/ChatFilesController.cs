using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.Domain.Storages;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Security.Claims;
using Microsoft.Extensions.Configuration; // Add for IConfiguration

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatFilesController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly IConfiguration _configuration; // Inject IConfiguration

        public ChatFilesController(IStorageService storageService, IConfiguration configuration)
        {
            _storageService = storageService;
            _configuration = configuration;
        }

        [HttpPost("upload-image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            if (!file.ContentType.StartsWith("image/"))
            {
                return BadRequest("Only image files are allowed.");
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var fid = await _storageService.UploadAnyFileAsync(stream, file.FileName, file.ContentType);
                    
                    var imageUrl = $"chatfiles/image/{fid}";
                    
                    return Ok(new { imageUrl });
                }
            }
            catch (Exception ex)
            {
                // TODO: Log the exception
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("image/{fid}")]
        public async Task<IActionResult> GetImage(string fid)
        {
            try
            {
                var (stream, contentType) = await _storageService.GetFileWithContentTypeAsync(fid);
                if (stream == null)
                {
                    return NotFound();
                }
                return File(stream, contentType ?? "application/octet-stream");
            }
            catch (Exception ex)
            {
                // TODO: Log the exception
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    }
}
