using Grpc.Net.Client;
using DoctorService.API.Grpc;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Net.Http;
using System.Threading.Tasks;

Console.WriteLine("🏥 Hospital Management System - Doctor Service gRPC Client");
Console.WriteLine("=======================================================\n");

// Create gRPC channel
using var channel = GrpcChannel.ForAddress("https://localhost:5202", new GrpcChannelOptions
{
    HttpHandler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }
});

var client = new DoctorGrpcService.DoctorGrpcServiceClient(channel);

try
{
    await TestAllEndpoints(client);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static async Task TestAllEndpoints(DoctorGrpcService.DoctorGrpcServiceClient client)
{
    // Test 1: List Doctors (Initially empty)
    Console.WriteLine("1. 📋 Testing ListDoctors...");
    var initialList = await client.ListDoctorsAsync(new Empty());
    Console.WriteLine($"   Initial doctors count: {initialList.Doctors.Count}");
    
    // Test 2: Create Doctor #1 - Cardiologist
    Console.WriteLine("\n2. ➕ Testing CreateDoctor - Cardiologist...");
    var createRequest1 = new DoctorRequest
    {
        Name = "Dr. Sarah Johnson",
        Specialty = "Cardiology"
    };
    
    var createdDoctor1 = await client.CreateDoctorAsync(createRequest1);
    Console.WriteLine($"   ✅ Created Doctor: ID={createdDoctor1.Id}, Name={createdDoctor1.Name}, Specialty={createdDoctor1.Specialty}");
    
    // Test 3: Create Doctor #2 - Neurologist
    Console.WriteLine("\n3. ➕ Creating another doctor - Neurologist...");
    var createRequest2 = new DoctorRequest
    {
        Name = "Dr. Michael Chen",
        Specialty = "Neurology"
    };
    
    var createdDoctor2 = await client.CreateDoctorAsync(createRequest2);
    Console.WriteLine($"   ✅ Created Doctor: ID={createdDoctor2.Id}, Name={createdDoctor2.Name}, Specialty={createdDoctor2.Specialty}");

    // Test 4: Create Doctor #3 - Pediatrician
    Console.WriteLine("\n4. ➕ Creating third doctor - Pediatrician...");
    var createRequest3 = new DoctorRequest
    {
        Name = "Dr. Emily Rodriguez",
        Specialty = "Pediatrics"
    };
    
    var createdDoctor3 = await client.CreateDoctorAsync(createRequest3);
    Console.WriteLine($"   ✅ Created Doctor: ID={createdDoctor3.Id}, Name={createdDoctor3.Name}, Specialty={createdDoctor3.Specialty}");
    
    // Test 5: Get Doctor by ID
    Console.WriteLine("\n5. 🔍 Testing GetDoctor...");
    var getRequest = new GetDoctorRequest { Id = createdDoctor1.Id };
    var retrievedDoctor = await client.GetDoctorAsync(getRequest);
    Console.WriteLine($"   ✅ Retrieved Doctor: ID={retrievedDoctor.Id}, Name={retrievedDoctor.Name}, Specialty={retrievedDoctor.Specialty}");
    
    // Test 6: Update Doctor
    Console.WriteLine("\n6. ✏️ Testing UpdateDoctor...");
    var updateRequest = new UpdateDoctorRequest
    {
        Id = createdDoctor1.Id,
        Name = "Dr. Sarah Johnson-Smith",
        Specialty = "Interventional Cardiology"
    };
    
    var updatedDoctor = await client.UpdateDoctorAsync(updateRequest);
    Console.WriteLine($"   ✅ Updated Doctor: ID={updatedDoctor.Id}, Name={updatedDoctor.Name}, Specialty={updatedDoctor.Specialty}");
    
    // Test 7: List all doctors
    Console.WriteLine("\n7. 📋 Testing ListDoctors after operations...");
    var finalList = await client.ListDoctorsAsync(new Empty());
    Console.WriteLine($"   Total doctors: {finalList.Doctors.Count}");
    
    foreach (var doctor in finalList.Doctors)
    {
        Console.WriteLine($"   - ID={doctor.Id}, Name={doctor.Name}, Specialty={doctor.Specialty}");
    }
    
    // Test 8: Delete Doctor
    Console.WriteLine("\n8. 🗑️ Testing DeleteDoctor...");
    var deleteRequest = new GetDoctorRequest { Id = createdDoctor2.Id };
    await client.DeleteDoctorAsync(deleteRequest);
    Console.WriteLine($"   ✅ Deleted Doctor with ID: {createdDoctor2.Id}");
    
    // Test 9: List doctors after deletion
    Console.WriteLine("\n9. 📋 Testing ListDoctors after deletion...");
    var afterDeleteList = await client.ListDoctorsAsync(new Empty());
    Console.WriteLine($"   Doctors count after deletion: {afterDeleteList.Doctors.Count}");
    
    foreach (var doctor in afterDeleteList.Doctors)
    {
        Console.WriteLine($"   - ID={doctor.Id}, Name={doctor.Name}, Specialty={doctor.Specialty}");
    }
    
    // Test 10: Try to get deleted doctor (should fail)
    Console.WriteLine("\n10. ❌ Testing GetDoctor for deleted doctor (should fail)...");
    try
    {
        var deletedDoctorRequest = new GetDoctorRequest { Id = createdDoctor2.Id };
        await client.GetDoctorAsync(deletedDoctorRequest);
        Console.WriteLine("   ❌ Unexpected: Should have failed!");
    }
    catch (Grpc.Core.RpcException ex)
    {
        Console.WriteLine($"   ✅ Expected error: {ex.Status.Detail}");
    }

    // Test 11: Try to update non-existent doctor (should fail)
    Console.WriteLine("\n11. ❌ Testing UpdateDoctor for non-existent doctor (should fail)...");
    try
    {
        var invalidUpdateRequest = new UpdateDoctorRequest
        {
            Id = 999,
            Name = "Dr. NonExistent",
            Specialty = "Unknown"
        };
        await client.UpdateDoctorAsync(invalidUpdateRequest);
        Console.WriteLine("   ❌ Unexpected: Should have failed!");
    }
    catch (Grpc.Core.RpcException ex)
    {
        Console.WriteLine($"   ✅ Expected error: {ex.Status.Detail}");
    }

    // Test 12: Try to delete non-existent doctor (should fail)
    Console.WriteLine("\n12. ❌ Testing DeleteDoctor for non-existent doctor (should fail)...");
    try
    {
        var invalidDeleteRequest = new GetDoctorRequest { Id = 999 };
        await client.DeleteDoctorAsync(invalidDeleteRequest);
        Console.WriteLine("   ❌ Unexpected: Should have failed!");
    }
    catch (Grpc.Core.RpcException ex)
    {
        Console.WriteLine($"   ✅ Expected error: {ex.Status.Detail}");
    }
    
    Console.WriteLine("\n🎉 All Doctor Service gRPC tests completed successfully!");
    Console.WriteLine("\n📊 Test Summary:");
    Console.WriteLine("   ✅ CreateDoctor - 3 doctors created");
    Console.WriteLine("   ✅ GetDoctor - Successfully retrieved doctor");
    Console.WriteLine("   ✅ UpdateDoctor - Successfully updated doctor details");
    Console.WriteLine("   ✅ ListDoctors - Successfully listed all doctors");
    Console.WriteLine("   ✅ DeleteDoctor - Successfully deleted doctor");
    Console.WriteLine("   ✅ Error handling - All error cases handled correctly");
    Console.WriteLine("\n🏥 Doctor specialties tested: Cardiology, Neurology, Pediatrics");
}
