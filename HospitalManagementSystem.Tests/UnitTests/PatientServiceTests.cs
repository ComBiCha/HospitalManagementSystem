using Xunit;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Application.DTOs;
using HospitalManagementSystem.Domain.Repositories;


public class PatientServiceTests
{
    [Fact]
    public async Task CreatePatientAsync_CreatesPatientCorrectly()
    {
        // Arrange
        var mockRepo = new Mock<IPatientRepository>();
        mockRepo.Setup(r => r.CreatePatientAsync(It.IsAny<Patient>()))
            .ReturnsAsync((Patient p) => p);

        var service = new PatientService(mockRepo.Object, null);

        var dto = new PatientCreateDto
        {
            Name = "Jane",
            Age = 30,
            Email = "jane@example.com",
            Identifiers = new List<PatientIdentifierDto>
            {
                new PatientIdentifierDto
                {
                    EHRSystem = EHRSystem.Epic,
                    ExternalId = "abc",
                    IdentifierType = "FHIR"
                }
            }
        };

        // Act
        var patient = await service.CreatePatientAsync(dto);

        // Assert
        Assert.Equal("Jane", patient.Name);
        Assert.Equal(30, patient.Age);
        Assert.Equal("jane@example.com", patient.Email);
        Assert.Single(patient.PatientIdentifiers);

        var identifier = patient.PatientIdentifiers.First();
        Assert.Equal("abc", identifier.ExternalId);
        Assert.Equal(EHRSystem.Epic, identifier.EHRSystem);
        Assert.Equal("FHIR", identifier.IdentifierType);
    }
}