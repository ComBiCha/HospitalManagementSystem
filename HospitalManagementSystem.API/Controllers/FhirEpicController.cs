using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Domain.Fhir;

[ApiController]
[Route("api/[controller]")]
public class FhirEpicController : ControllerBase
{
    private readonly EhrFhirApplicationService _ehrFhirService;

    public FhirEpicController(EhrFhirApplicationService ehrFhirService)
    {
        _ehrFhirService = ehrFhirService;
    }

    [HttpGet("patient/{id}")]
    public async Task<IActionResult> GetPatientDemographics(string id, [FromQuery] EHRSystem ehrSystem = EHRSystem.Epic)
    {
        var result = await _ehrFhirService.GetPatientDemographicsAsync(id, ehrSystem);
        return Ok(result);
    }

    [HttpGet("patient/{patientId}/external-history")]
    public async Task<IActionResult> GetExternalHistory(string patientId, [FromQuery] EHRSystem ehrSystem = EHRSystem.Epic)
    {
        try
        {
            var history = await _ehrFhirService.GetExternalPatientHistoryAsync(patientId, ehrSystem);
            return Ok(history);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching external patient history.", details = ex.Message });
        }
    }

    [HttpGet("patient/{patientId}/external-history-selective")]
    public async Task<IActionResult> GetExternalHistorySelective(string patientId, [FromQuery] List<string> resourceTypes, [FromQuery] EHRSystem ehrSystem = EHRSystem.Epic)
    {
        try
        {
            var history = await _ehrFhirService.GetExternalPatientHistoryAsync(patientId, ehrSystem, resourceTypes);
            return Ok(history);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while fetching external patient history.", details = ex.Message });
        }
    }
}