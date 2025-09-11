using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Application.Services;

[ApiController]
[Route("api/[controller]")]
public class FhirEpicController : ControllerBase
{
    private readonly FhirEpicIntegrationService _fhirEpicService;

    public FhirEpicController(FhirEpicIntegrationService fhirEpicService)
    {
        _fhirEpicService = fhirEpicService;
    }

    [HttpGet("patient/{id}")]
    public async Task<IActionResult> GetPatientDemographics(string id)
    {
        var result = await _fhirEpicService.GetPatientDemographicsAsync(id);
        return Ok(result);
    }

    [HttpGet("appointments/{patientId}")]
    public async Task<IActionResult> GetAppointments(string patientId)
    {
        var result = await _fhirEpicService.GetAppointmentsAsync(patientId);
        return Ok(result);
    }

    [HttpGet("medications/{patientId}")]
    public async Task<IActionResult> GetMedications(string patientId)
    {
        var result = await _fhirEpicService.GetMedicationsAsync(patientId);
        return Ok(result);
    }
}