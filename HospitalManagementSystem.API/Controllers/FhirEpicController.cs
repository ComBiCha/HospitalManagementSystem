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
}