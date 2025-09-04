using Grpc.Net.Client;
using PatientService.API.Grpc;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Net.Http;
using System.Threading.Tasks;

Console.WriteLine("Hospital Management System - Patient Service gRPC Client");
Console.WriteLine("========================================================\n");

// Create gRPC channel
using var channel = GrpcChannel.ForAddress("https://localhost:5101", new GrpcChannelOptions
{
    HttpHandler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }
});

var client = new PatientGrpcService.PatientGrpcServiceClient(channel);

try
{
    await TestAllEndpoints(client);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static async Task TestAllEndpoints(PatientGrpcService.PatientGrpcServiceClient client)
{
    // Test 1: List Patients (Initially empty)
    Console.WriteLine("1.Testing ListPatients...");
    var initialList = await client.ListPatientsAsync(new Empty());
    Console.WriteLine($"   Initial patients count: {initialList.Patients.Count}");
    
    // Test 2: Create Patient
    Console.WriteLine("\n2.Testing CreatePatient...");
    var createRequest = new PatientRequest
    {
        Name = "John Doe",
        Age = 30,
        Email = "john.doe@email.com"
    };
    
    var createdPatient = await client.CreatePatientAsync(createRequest);
    Console.WriteLine($"   Created Patient: ID={createdPatient.Id}, Name={createdPatient.Name}, Age={createdPatient.Age}, Email={createdPatient.Email}");
    
    // Test 3: Create another patient
    Console.WriteLine("\n3. Creating another patient...");
    var createRequest2 = new PatientRequest
    {
        Name = "Jane Smith",
        Age = 25,
        Email = "jane.smith@email.com"
    };
    
    var createdPatient2 = await client.CreatePatientAsync(createRequest2);
    Console.WriteLine($"   Created Patient: ID={createdPatient2.Id}, Name={createdPatient2.Name}, Age={createdPatient2.Age}, Email={createdPatient2.Email}");
    
    // Test 4: Get Patient by ID
    Console.WriteLine("\n4. Testing GetPatient...");
    var getRequest = new GetPatientRequest { Id = createdPatient.Id };
    var retrievedPatient = await client.GetPatientAsync(getRequest);
    Console.WriteLine($"   Retrieved Patient: ID={retrievedPatient.Id}, Name={retrievedPatient.Name}, Age={retrievedPatient.Age}, Email={retrievedPatient.Email}");
    
    // Test 5: Update Patient
    Console.WriteLine("\n5. Testing UpdatePatient...");
    var updateRequest = new UpdatePatientRequest
    {
        Id = createdPatient.Id,
        Name = "John Updated",
        Age = 31,
        Email = "john.updated@email.com"
    };
    
    var updatedPatient = await client.UpdatePatientAsync(updateRequest);
    Console.WriteLine($"   Updated Patient: ID={updatedPatient.Id}, Name={updatedPatient.Name}, Age={updatedPatient.Age}, Email={updatedPatient.Email}");
    
    // Test 6: List all patients
    Console.WriteLine("\n6. Testing ListPatients after operations...");
    var finalList = await client.ListPatientsAsync(new Empty());
    Console.WriteLine($"   Total patients: {finalList.Patients.Count}");
    
    foreach (var patient in finalList.Patients)
    {
        Console.WriteLine($"   - ID={patient.Id}, Name={patient.Name}, Age={patient.Age}, Email={patient.Email}");
    }
    
    // Test 7: Delete Patient
    Console.WriteLine("\n7. Testing DeletePatient...");
    var deleteRequest = new GetPatientRequest { Id = createdPatient2.Id };
    await client.DeletePatientAsync(deleteRequest);
    Console.WriteLine($"   Deleted Patient with ID: {createdPatient2.Id}");
    
    // Test 8: List patients after deletion
    Console.WriteLine("\n8. Testing ListPatients after deletion...");
    var afterDeleteList = await client.ListPatientsAsync(new Empty());
    Console.WriteLine($"   Patients count after deletion: {afterDeleteList.Patients.Count}");
    
    foreach (var patient in afterDeleteList.Patients)
    {
        Console.WriteLine($"   - ID={patient.Id}, Name={patient.Name}, Age={patient.Age}, Email={patient.Email}");
    }
    
    // Test 9: Try to get deleted patient (should fail)
    Console.WriteLine("\n9. Testing GetPatient for deleted patient (should fail)...");
    try
    {
        var deletedPatientRequest = new GetPatientRequest { Id = createdPatient2.Id };
        await client.GetPatientAsync(deletedPatientRequest);
        Console.WriteLine("   Unexpected: Should have failed!");
    }
    catch (Grpc.Core.RpcException ex)
    {
        Console.WriteLine($"   Expected error: {ex.Status.Detail}");
    }
    
    Console.WriteLine("\nAll tests completed successfully!");
}
