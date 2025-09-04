using Grpc.Core;
using PatientService.API.Grpc;
using PatientService.API.Models;
using PatientService.API.Repositories;
using Google.Protobuf.WellKnownTypes;

namespace PatientService.API.Services
{
    public class PatientGrpcService : Grpc.PatientGrpcService.PatientGrpcServiceBase
    {
        private readonly IPatientRepository _patientRepository;
        private readonly ILogger<PatientGrpcService> _logger;

        public PatientGrpcService(IPatientRepository patientRepository, ILogger<PatientGrpcService> logger)
        {
            _patientRepository = patientRepository;
            _logger = logger;
        }

        public override async Task<PatientResponse> CreatePatient(PatientRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received CreatePatient gRPC request: Name={Name}, Age={Age}, Email={Email}", 
                request.Name, request.Age, request.Email);

            try
            {
                var patient = new Patient
                {
                    Name = request.Name,
                    Age = request.Age,
                    Email = request.Email
                };

                var createdPatient = await _patientRepository.CreateAsync(patient);

                return new PatientResponse
                {
                    Id = createdPatient.Id,
                    Name = createdPatient.Name,
                    Age = createdPatient.Age,
                    Email = createdPatient.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CreatePatient gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error creating patient: {ex.Message}"));
            }
        }

        public override async Task<PatientResponse> GetPatient(GetPatientRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received GetPatient gRPC request for ID: {PatientId}", request.Id);

            try
            {
                var patient = await _patientRepository.GetByIdAsync(request.Id);

                if (patient == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Patient with ID {request.Id} not found"));
                }

                return new PatientResponse
                {
                    Id = patient.Id,
                    Name = patient.Name,
                    Age = patient.Age,
                    Email = patient.Email
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetPatient gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving patient: {ex.Message}"));
            }
        }

        public override async Task<PatientResponse> UpdatePatient(UpdatePatientRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received UpdatePatient gRPC request for ID: {PatientId}", request.Id);

            try
            {
                var patient = new Patient
                {
                    Id = request.Id,
                    Name = request.Name,
                    Age = request.Age,
                    Email = request.Email
                };

                var updatedPatient = await _patientRepository.UpdateAsync(patient);

                if (updatedPatient == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Patient with ID {request.Id} not found"));
                }

                return new PatientResponse
                {
                    Id = updatedPatient.Id,
                    Name = updatedPatient.Name,
                    Age = updatedPatient.Age,
                    Email = updatedPatient.Email
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UpdatePatient gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error updating patient: {ex.Message}"));
            }
        }

        public override async Task<Empty> DeletePatient(GetPatientRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received DeletePatient gRPC request for ID: {PatientId}", request.Id);

            try
            {
                var deleted = await _patientRepository.DeleteAsync(request.Id);

                if (!deleted)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Patient with ID {request.Id} not found"));
                }

                return new Empty();
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DeletePatient gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error deleting patient: {ex.Message}"));
            }
        }

        public override async Task<PatientList> ListPatients(Empty request, ServerCallContext context)
        {
            _logger.LogInformation("Received ListPatients gRPC request");

            try
            {
                var patients = await _patientRepository.GetAllAsync();

                var response = new PatientList();
                
                foreach (var patient in patients)
                {
                    response.Patients.Add(new PatientResponse
                    {
                        Id = patient.Id,
                        Name = patient.Name,
                        Age = patient.Age,
                        Email = patient.Email
                    });
                }

                _logger.LogInformation("Successfully processed ListPatients gRPC request with {Count} patients", response.Patients.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ListPatients gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving patients: {ex.Message}"));
            }
        }
    }
}


