using AppointmentService.API.Grpc.Clients;

namespace AppointmentService.API.Services.GrpcClients
{
    public interface IDoctorGrpcClient
    {
        Task<DoctorResponse?> GetDoctorAsync(int doctorId);
        Task<bool> ValidateDoctorExistsAsync(int doctorId);
    }
}
