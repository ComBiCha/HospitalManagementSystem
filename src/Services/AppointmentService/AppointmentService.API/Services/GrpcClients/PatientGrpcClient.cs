using AppointmentService.API.Grpc.Clients;
using Grpc.Core;

namespace AppointmentService.API.Services.GrpcClients
{
    public class PatientGrpcClient : IPatientGrpcClient
    {
        private readonly PatientGrpcService.PatientGrpcServiceClient _client;
        private readonly ILogger<PatientGrpcClient> _logger;

        public PatientGrpcClient(PatientGrpcService.PatientGrpcServiceClient client, ILogger<PatientGrpcClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<PatientResponse?> GetPatientAsync(int patientId)
        {
            try
            {
                _logger.LogInformation("Calling Patient Service to get patient {PatientId}", patientId);
                
                var request = new GetPatientRequest { Id = patientId };
                var response = await _client.GetPatientAsync(request);
                
                _logger.LogInformation("Successfully retrieved patient {PatientId}: {PatientName}", patientId, response.Name);
                return response;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Patient {PatientId} not found", patientId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Patient Service for patient {PatientId}", patientId);
                throw;
            }
        }

        public async Task<bool> ValidatePatientExistsAsync(int patientId)
        {
            try
            {
                var patient = await GetPatientAsync(patientId);
                return patient != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating patient {PatientId}", patientId);
                return false;
            }
        }
    }
}
