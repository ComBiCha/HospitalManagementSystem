using AppointmentService.API.Grpc.Clients;
using Grpc.Core;

namespace AppointmentService.API.Services.GrpcClients
{
    public class DoctorGrpcClient : IDoctorGrpcClient
    {
        private readonly DoctorGrpcService.DoctorGrpcServiceClient _client;
        private readonly ILogger<DoctorGrpcClient> _logger;

        public DoctorGrpcClient(DoctorGrpcService.DoctorGrpcServiceClient client, ILogger<DoctorGrpcClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DoctorResponse?> GetDoctorAsync(int doctorId)
        {
            try
            {
                _logger.LogInformation("Calling Doctor Service to get doctor {DoctorId}", doctorId);
                
                var request = new GetDoctorRequest { Id = doctorId };
                var response = await _client.GetDoctorAsync(request);
                
                _logger.LogInformation("Successfully retrieved doctor {DoctorId}: {DoctorName} ({Specialty})", 
                    doctorId, response.Name, response.Specialty);
                return response;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Doctor {DoctorId} not found", doctorId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Doctor Service for doctor {DoctorId}", doctorId);
                throw;
            }
        }

        public async Task<bool> ValidateDoctorExistsAsync(int doctorId)
        {
            try
            {
                var doctor = await GetDoctorAsync(doctorId);
                return doctor != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating doctor {DoctorId}", doctorId);
                return false;
            }
        }
    }
}
