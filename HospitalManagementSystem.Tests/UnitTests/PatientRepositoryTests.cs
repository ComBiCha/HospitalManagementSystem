using Xunit;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Infrastructure.Repositories;
using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Infrastructure.Persistence;
using Moq;
using Microsoft.Extensions.Logging;

public class PatientRepositoryTests
{
    [Fact]
    public async Task CreatePatientAsync_SavesToDb()
    {
        var options = new DbContextOptionsBuilder<HospitalDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;
        using var context = new HospitalDbContext(options);
        var mockLogger = new Mock<ILogger<PatientRepository>>();
        var repo = new PatientRepository(context, mockLogger.Object);

        var patient = new Patient { Name = "Test", Age = 20, Email = "test@example.com" };
        var result = await repo.CreatePatientAsync(patient);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(1, await context.Patients.CountAsync());
    }
}