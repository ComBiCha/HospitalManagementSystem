using AppointmentService.API.Grpc.Clients;

namespace AppointmentService.API.Services.GrpcClients
{
    public interface IPatientGrpcClient
    {
        Task<PatientResponse?> GetPatientAsync(int patientId);
        Task<bool> ValidatePatientExistsAsync(int patientId);
    }
}
