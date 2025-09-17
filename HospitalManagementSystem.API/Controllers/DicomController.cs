using HospitalManagementSystem.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/dicom")]
    public class DicomController : ControllerBase
    {
        private readonly DicomApplicationService _dicomAppService;

        public DicomController(DicomApplicationService dicomAppService)
        {
            _dicomAppService = dicomAppService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDicom([FromForm] IFormFile dicomFile)
        {
            var filePath = Path.GetTempFileName();
            using (var stream = System.IO.File.Create(filePath))
            {
                await dicomFile.CopyToAsync(stream);
            }
            await _dicomAppService.UploadDicomAsync(filePath);
            return Ok("Uploaded");
        }

        [HttpGet("patients")]
        public async Task<IActionResult> GetAllPatients()
            => Ok(await _dicomAppService.GetAllPatientsAsync());

        [HttpGet("patients/{patientId}")]
        public async Task<IActionResult> GetPatientDetails(string patientId)
            => Ok(await _dicomAppService.GetPatientDetailsAsync(patientId));

        [HttpGet("studies/{studyId}")]
        public async Task<IActionResult> GetStudyDetails(string studyId)
            => Ok(await _dicomAppService.GetStudyDetailsAsync(studyId));

        [HttpGet("series/{seriesId}")]
        public async Task<IActionResult> GetSeriesDetails(string seriesId)
            => Ok(await _dicomAppService.GetSeriesDetailsAsync(seriesId));

        [HttpGet("instances/{instanceId}")]
        public async Task<IActionResult> GetInstanceDetails(string instanceId)
            => Ok(await _dicomAppService.GetInstanceDetailsAsync(instanceId));

        [HttpGet("instances/{instanceId}/download")]
        public async Task<IActionResult> DownloadInstanceFile(string instanceId)
        {
            var savePath = Path.GetTempFileName();
            await _dicomAppService.DownloadInstanceFileAsync(instanceId, savePath);
            var bytes = System.IO.File.ReadAllBytes(savePath);
            return File(bytes, "application/dicom", $"{instanceId}.dcm");
        }

        [HttpGet("instances/{instanceId}/preview")]
        public async Task<IActionResult> DownloadInstanceAsJpeg(string instanceId)
        {
            var savePath = Path.GetTempFileName();
            await _dicomAppService.DownloadInstanceAsJpegAsync(instanceId, savePath);
            var bytes = System.IO.File.ReadAllBytes(savePath);
            return File(bytes, "image/jpeg", $"{instanceId}.jpg");
        }
    }
}