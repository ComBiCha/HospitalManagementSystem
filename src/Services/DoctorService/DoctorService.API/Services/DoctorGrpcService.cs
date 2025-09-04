using Grpc.Core;
using DoctorService.API.Grpc;
using DoctorService.API.Models;
using DoctorService.API.Repositories;
using Google.Protobuf.WellKnownTypes;

namespace DoctorService.API.Services
{
    public class DoctorGrpcService : Grpc.DoctorGrpcService.DoctorGrpcServiceBase
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly ILogger<DoctorGrpcService> _logger;

        public DoctorGrpcService(IDoctorRepository doctorRepository, ILogger<DoctorGrpcService> logger)
        {
            _doctorRepository = doctorRepository;
            _logger = logger;
        }

        public override async Task<DoctorResponse> CreateDoctor(DoctorRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received CreateDoctor gRPC request: Name={Name}, Specialty={Specialty}", 
                request.Name, request.Specialty);

            try
            {
                var doctor = new Doctor
                {
                    Name = request.Name,
                    Specialty = request.Specialty
                };

                var createdDoctor = await _doctorRepository.CreateAsync(doctor);

                return new DoctorResponse
                {
                    Id = createdDoctor.Id,
                    Name = createdDoctor.Name,
                    Specialty = createdDoctor.Specialty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CreateDoctor gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error creating doctor: {ex.Message}"));
            }
        }

        public override async Task<DoctorResponse> GetDoctor(GetDoctorRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received GetDoctor gRPC request for ID: {DoctorId}", request.Id);

            try
            {
                var doctor = await _doctorRepository.GetByIdAsync(request.Id);

                if (doctor == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Doctor with ID {request.Id} not found"));
                }

                return new DoctorResponse
                {
                    Id = doctor.Id,
                    Name = doctor.Name,
                    Specialty = doctor.Specialty
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetDoctor gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving doctor: {ex.Message}"));
            }
        }

        public override async Task<DoctorResponse> UpdateDoctor(UpdateDoctorRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received UpdateDoctor gRPC request for ID: {DoctorId}", request.Id);

            try
            {
                var doctor = new Doctor
                {
                    Id = request.Id,
                    Name = request.Name,
                    Specialty = request.Specialty
                };

                var updatedDoctor = await _doctorRepository.UpdateAsync(doctor);

                if (updatedDoctor == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Doctor with ID {request.Id} not found"));
                }

                return new DoctorResponse
                {
                    Id = updatedDoctor.Id,
                    Name = updatedDoctor.Name,
                    Specialty = updatedDoctor.Specialty
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UpdateDoctor gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error updating doctor: {ex.Message}"));
            }
        }

        public override async Task<Empty> DeleteDoctor(GetDoctorRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Received DeleteDoctor gRPC request for ID: {DoctorId}", request.Id);

            try
            {
                var deleted = await _doctorRepository.DeleteAsync(request.Id);

                if (!deleted)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"Doctor with ID {request.Id} not found"));
                }

                return new Empty();
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DeleteDoctor gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error deleting doctor: {ex.Message}"));
            }
        }

        public override async Task<DoctorList> ListDoctors(Empty request, ServerCallContext context)
        {
            _logger.LogInformation("Received ListDoctors gRPC request");

            try
            {
                var doctors = await _doctorRepository.GetAllAsync();

                var response = new DoctorList();
                
                foreach (var doctor in doctors)
                {
                    response.Doctors.Add(new DoctorResponse
                    {
                        Id = doctor.Id,
                        Name = doctor.Name,
                        Specialty = doctor.Specialty
                    });
                }

                _logger.LogInformation("Successfully processed ListDoctors gRPC request with {Count} doctors", response.Doctors.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ListDoctors gRPC request");
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving doctors: {ex.Message}"));
            }
        }
    }
}
