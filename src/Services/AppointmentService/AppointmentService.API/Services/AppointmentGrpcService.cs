using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;
using AppointmentService.API.Models;
using AppointmentService.API.Repositories;
using AppointmentService.API.Services.GrpcClients;
using AppointmentService.API.Services.RabbitMQ;
using AppointmentService.API.Grpc; // ✅ Correct namespace from proto file

namespace AppointmentService.API.Services
{
    /// <summary>
    /// gRPC service for appointment operations with JWT authentication
    /// Follows Clean Architecture principles with proper separation of concerns
    /// </summary>
    [Authorize] // Require JWT authentication for all gRPC operations
    public class AppointmentGrpcService : AppointmentService.API.Grpc.AppointmentGrpcService.AppointmentGrpcServiceBase // ✅ Fixed base class
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPatientGrpcClient _patientGrpcClient;
        private readonly IDoctorGrpcClient _doctorGrpcClient;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<AppointmentGrpcService> _logger;

        public AppointmentGrpcService(
            IAppointmentRepository appointmentRepository,
            IPatientGrpcClient patientGrpcClient,
            IDoctorGrpcClient doctorGrpcClient,
            IRabbitMQService rabbitMQService,
            ILogger<AppointmentGrpcService> logger)
        {
            _appointmentRepository = appointmentRepository;
            _patientGrpcClient = patientGrpcClient;
            _doctorGrpcClient = doctorGrpcClient;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        /// <summary>
        /// Get appointment by ID with role-based authorization
        /// </summary>
        public override async Task<AppointmentResponse> GetAppointment(
            GetAppointmentRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC GetAppointment called by user {UserId} with role {Role} for appointment {AppointmentId}", 
                    currentUserId, currentUserRole, request.Id);

                var appointment = await _appointmentRepository.GetByIdAsync(request.Id);
                if (appointment == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, 
                        $"Appointment with ID {request.Id} not found"));
                }

                // Role-based access control
                if (!CanAccessAppointment(appointment, currentUserId, currentUserRole, context))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, 
                        "You don't have permission to access this appointment"));
                }

                // Get related patient and doctor information via gRPC clients
                var patient = await _patientGrpcClient.GetPatientAsync(appointment.PatientId);
                var doctor = await _doctorGrpcClient.GetDoctorAsync(appointment.DoctorId);

                return new AppointmentResponse
                {
                    Id = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    Date = Timestamp.FromDateTime(appointment.Date),
                    Status = appointment.Status,
                    PatientName = patient?.Name ?? "Unknown Patient",
                    DoctorName = doctor?.Name ?? "Unknown Doctor",
                    DoctorSpecialty = doctor?.Specialty ?? "Unknown Specialty"
                };
            }
            catch (RpcException)
            {
                throw; // Re-throw gRPC exceptions as-is
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointment {AppointmentId} via gRPC", request.Id);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// Create new appointment with JWT authorization and RabbitMQ event publishing
        /// </summary>
        [Authorize(Roles = "Admin,Patient,Doctor")]
        public override async Task<AppointmentResponse> CreateAppointment(
            AppointmentRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC CreateAppointment called by user {UserId} with role {Role}: PatientId={PatientId}, DoctorId={DoctorId}", 
                    currentUserId, currentUserRole, request.PatientId, request.DoctorId);

                // Patients can only create appointments for themselves
                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId(context);
                    if (userPatientId != request.PatientId)
                    {
                        throw new RpcException(new Status(StatusCode.PermissionDenied, 
                            "You can only create appointments for yourself"));
                    }
                }

                // Convert Timestamp to DateTime
                var appointmentDate = request.Date.ToDateTime();
                var utcDate = appointmentDate.Kind == DateTimeKind.Utc 
                    ? appointmentDate 
                    : DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

                if (utcDate <= DateTime.UtcNow)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, 
                        "Appointment date must be in the future"));
                }

                // Validate patient exists via gRPC client
                var patient = await _patientGrpcClient.GetPatientAsync(request.PatientId);
                if (patient == null)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, 
                        $"Patient with ID {request.PatientId} not found"));
                }

                // Validate doctor exists via gRPC client
                var doctor = await _doctorGrpcClient.GetDoctorAsync(request.DoctorId);
                if (doctor == null)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, 
                        $"Doctor with ID {request.DoctorId} not found"));
                }

                // Check for conflicting appointments
                var hasConflict = await _appointmentRepository.HasConflictingAppointmentAsync(request.DoctorId, utcDate);
                if (hasConflict)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists, 
                        "Doctor already has an appointment at this time"));
                }

                var appointment = new Appointment
                {
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    Date = utcDate,
                    Status = string.IsNullOrEmpty(request.Status) ? "Scheduled" : request.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdAppointment = await _appointmentRepository.CreateAsync(appointment);

                // Publish event to RabbitMQ with user context
                await _rabbitMQService.PublishAppointmentCreatedAsync(new
                {
                    AppointmentId = createdAppointment.Id,
                    PatientId = createdAppointment.PatientId,
                    PatientName = patient.Name,
                    DoctorId = createdAppointment.DoctorId,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty,
                    Date = createdAppointment.Date,
                    Status = createdAppointment.Status,
                    CreatedAt = createdAppointment.CreatedAt,
                    CreatedByUserId = currentUserId,
                    CreatedByRole = currentUserRole,
                    Source = "gRPC"
                });

                _logger.LogInformation("Appointment {AppointmentId} created via gRPC by user {UserId} with role {Role}", 
                    createdAppointment.Id, currentUserId, currentUserRole);

                return new AppointmentResponse
                {
                    Id = createdAppointment.Id,
                    PatientId = createdAppointment.PatientId,
                    DoctorId = createdAppointment.DoctorId,
                    Date = Timestamp.FromDateTime(createdAppointment.Date),
                    Status = createdAppointment.Status,
                    PatientName = patient.Name,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment via gRPC");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// Update appointment with JWT authorization and RabbitMQ event publishing
        /// </summary>
        [Authorize(Roles = "Admin,Doctor")]
        public override async Task<AppointmentResponse> UpdateAppointment(
            UpdateAppointmentRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC UpdateAppointment called by user {UserId} with role {Role} for appointment {AppointmentId}", 
                    currentUserId, currentUserRole, request.Id);

                var existingAppointment = await _appointmentRepository.GetByIdAsync(request.Id);
                if (existingAppointment == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, 
                        $"Appointment with ID {request.Id} not found"));
                }

                // Role-based access control
                if (!CanAccessAppointment(existingAppointment, currentUserId, currentUserRole, context))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, 
                        "You don't have permission to update this appointment"));
                }

                // Convert Timestamp to DateTime
                var appointmentDate = request.Date.ToDateTime();
                var utcDate = appointmentDate.Kind == DateTimeKind.Utc 
                    ? appointmentDate 
                    : DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

                // Validate patient and doctor exist
                var patient = await _patientGrpcClient.GetPatientAsync(request.PatientId);
                var doctor = await _doctorGrpcClient.GetDoctorAsync(request.DoctorId);

                if (patient == null)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, 
                        $"Patient with ID {request.PatientId} not found"));
                }

                if (doctor == null)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, 
                        $"Doctor with ID {request.DoctorId} not found"));
                }

                // Check for conflicting appointments (excluding current appointment)
                var hasConflict = await _appointmentRepository.HasConflictingAppointmentAsync(request.DoctorId, utcDate, request.Id);
                if (hasConflict)
                {
                    throw new RpcException(new Status(StatusCode.AlreadyExists, 
                        "Doctor already has an appointment at this time"));
                }

                var updatedAppointment = new Appointment
                {
                    Id = request.Id,
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    Date = utcDate,
                    Status = request.Status,
                    CreatedAt = existingAppointment.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await _appointmentRepository.UpdateAsync(updatedAppointment);
                if (result == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, 
                        $"Failed to update appointment with ID {request.Id}"));
                }

                // Publish event to RabbitMQ
                await _rabbitMQService.PublishAppointmentUpdatedAsync(new
                {
                    AppointmentId = result.Id,
                    PatientId = result.PatientId,
                    PatientName = patient.Name,
                    DoctorId = result.DoctorId,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty,
                    Date = result.Date,
                    Status = result.Status,
                    UpdatedAt = result.UpdatedAt,
                    UpdatedByUserId = currentUserId,
                    UpdatedByRole = currentUserRole,
                    Source = "gRPC"
                });

                _logger.LogInformation("Appointment {AppointmentId} updated via gRPC by user {UserId} with role {Role}", 
                    request.Id, currentUserId, currentUserRole);

                return new AppointmentResponse
                {
                    Id = result.Id,
                    PatientId = result.PatientId,
                    DoctorId = result.DoctorId,
                    Date = Timestamp.FromDateTime(result.Date),
                    Status = result.Status,
                    PatientName = patient.Name,
                    DoctorName = doctor.Name,
                    DoctorSpecialty = doctor.Specialty
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment {AppointmentId} via gRPC", request.Id);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// Delete appointment with JWT authorization and RabbitMQ event publishing
        /// </summary>
        [Authorize(Roles = "Admin,Doctor,Patient")]
        public override async Task<Empty> DeleteAppointment(
            GetAppointmentRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC DeleteAppointment called by user {UserId} with role {Role} for appointment {AppointmentId}", 
                    currentUserId, currentUserRole, request.Id);

                var appointment = await _appointmentRepository.GetByIdAsync(request.Id);
                if (appointment == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, 
                        $"Appointment with ID {request.Id} not found"));
                }

                // Role-based access control
                if (!CanAccessAppointment(appointment, currentUserId, currentUserRole, context))
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, 
                        "You don't have permission to cancel this appointment"));
                }

                var deleted = await _appointmentRepository.DeleteAsync(request.Id);
                if (!deleted)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, 
                        $"Failed to delete appointment with ID {request.Id}"));
                }

                // Publish event to RabbitMQ
                await _rabbitMQService.PublishAppointmentCancelledAsync(new
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    Date = appointment.Date,
                    Status = "Cancelled",
                    CancelledAt = DateTime.UtcNow,
                    CancelledByUserId = currentUserId,
                    CancelledByRole = currentUserRole,
                    Source = "gRPC"
                });

                _logger.LogInformation("Appointment {AppointmentId} cancelled via gRPC by user {UserId} with role {Role}", 
                    request.Id, currentUserId, currentUserRole);

                return new Empty();
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting appointment {AppointmentId} via gRPC", request.Id);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// List all appointments (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        public override async Task<AppointmentList> ListAppointments(
            Empty request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC ListAppointments called by user {UserId} with role {Role}", 
                    currentUserId, currentUserRole);

                var appointments = await _appointmentRepository.GetAllAsync();
                var response = new AppointmentList();

                foreach (var appointment in appointments)
                {
                    var patient = await _patientGrpcClient.GetPatientAsync(appointment.PatientId);
                    var doctor = await _doctorGrpcClient.GetDoctorAsync(appointment.DoctorId);

                    response.Appointments.Add(new AppointmentResponse
                    {
                        Id = appointment.Id,
                        PatientId = appointment.PatientId,
                        DoctorId = appointment.DoctorId,
                        Date = Timestamp.FromDateTime(appointment.Date),
                        Status = appointment.Status,
                        PatientName = patient?.Name ?? "Unknown Patient",
                        DoctorName = doctor?.Name ?? "Unknown Doctor",
                        DoctorSpecialty = doctor?.Specialty ?? "Unknown Specialty"
                    });
                }

                _logger.LogInformation("Retrieved {Count} appointments via gRPC", response.Appointments.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing appointments via gRPC");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// Get appointments by doctor ID with role-based authorization
        /// </summary>
        [Authorize(Roles = "Admin,Doctor")]
        public override async Task<AppointmentList> GetAppointmentsByDoctor(
            GetAppointmentsByDoctorRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC GetAppointmentsByDoctor called by user {UserId} with role {Role} for doctor {DoctorId}", 
                    currentUserId, currentUserRole, request.DoctorId);

                // Doctors can only view their own appointments
                if (currentUserRole == "Doctor")
                {
                    var userDoctorId = GetCurrentUserDoctorId(context);
                    if (userDoctorId != request.DoctorId)
                    {
                        throw new RpcException(new Status(StatusCode.PermissionDenied, 
                            "You can only view your own appointments"));
                    }
                }

                var appointments = await _appointmentRepository.GetByDoctorIdAsync(request.DoctorId);
                var response = new AppointmentList();

                foreach (var appointment in appointments)
                {
                    var patient = await _patientGrpcClient.GetPatientAsync(appointment.PatientId);
                    var doctor = await _doctorGrpcClient.GetDoctorAsync(appointment.DoctorId);

                    response.Appointments.Add(new AppointmentResponse
                    {
                        Id = appointment.Id,
                        PatientId = appointment.PatientId,
                        DoctorId = appointment.DoctorId,
                        Date = Timestamp.FromDateTime(appointment.Date),
                        Status = appointment.Status,
                        PatientName = patient?.Name ?? "Unknown Patient",
                        DoctorName = doctor?.Name ?? "Unknown Doctor",
                        DoctorSpecialty = doctor?.Specialty ?? "Unknown Specialty"
                    });
                }

                _logger.LogInformation("Retrieved {Count} appointments for doctor {DoctorId} via gRPC", 
                    response.Appointments.Count, request.DoctorId);

                return response;
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for doctor {DoctorId} via gRPC", request.DoctorId);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        /// <summary>
        /// Get appointments by patient ID with role-based authorization
        /// </summary>
        public override async Task<AppointmentList> GetAppointmentsByPatient(
            GetAppointmentsByPatientRequest request, ServerCallContext context)
        {
            try
            {
                var currentUserId = GetCurrentUserId(context);
                var currentUserRole = GetCurrentUserRole(context);

                _logger.LogInformation("gRPC GetAppointmentsByPatient called by user {UserId} with role {Role} for patient {PatientId}", 
                    currentUserId, currentUserRole, request.PatientId);

                // Patients can only view their own appointments
                if (currentUserRole == "Patient")
                {
                    var userPatientId = GetCurrentUserPatientId(context);
                    if (userPatientId != request.PatientId)
                    {
                        throw new RpcException(new Status(StatusCode.PermissionDenied, 
                            "You can only view your own appointments"));
                    }
                }

                var appointments = await _appointmentRepository.GetByPatientIdAsync(request.PatientId);
                var response = new AppointmentList();

                foreach (var appointment in appointments)
                {
                    var patient = await _patientGrpcClient.GetPatientAsync(appointment.PatientId);
                    var doctor = await _doctorGrpcClient.GetDoctorAsync(appointment.DoctorId);

                    response.Appointments.Add(new AppointmentResponse
                    {
                        Id = appointment.Id,
                        PatientId = appointment.PatientId,
                        DoctorId = appointment.DoctorId,
                        Date = Timestamp.FromDateTime(appointment.Date),
                        Status = appointment.Status,
                        PatientName = patient?.Name ?? "Unknown Patient",
                        DoctorName = doctor?.Name ?? "Unknown Doctor",
                        DoctorSpecialty = doctor?.Specialty ?? "Unknown Specialty"
                    });
                }

                _logger.LogInformation("Retrieved {Count} appointments for patient {PatientId} via gRPC", 
                    response.Appointments.Count, request.PatientId);

                return response;
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting appointments for patient {PatientId} via gRPC", request.PatientId);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        // Helper methods for JWT claims extraction in gRPC context
        private int GetCurrentUserId(ServerCallContext context)
        {
            var userIdClaim = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        private string GetCurrentUserRole(ServerCallContext context)
        {
            return context.GetHttpContext().User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private int? GetCurrentUserPatientId(ServerCallContext context)
        {
            var patientIdClaim = context.GetHttpContext().User.FindFirst("PatientId")?.Value;
            return int.TryParse(patientIdClaim, out int patientId) ? patientId : null;
        }

        private int? GetCurrentUserDoctorId(ServerCallContext context)
        {
            var doctorIdClaim = context.GetHttpContext().User.FindFirst("DoctorId")?.Value;
            return int.TryParse(doctorIdClaim, out int doctorId) ? doctorId : null;
        }

        // Authorization helper method for gRPC context
        private bool CanAccessAppointment(Appointment appointment, int userId, string userRole, ServerCallContext context)
        {
            switch (userRole)
            {
                case "Admin":
                    return true;

                case "Doctor":
                    var userDoctorId = GetCurrentUserDoctorId(context);
                    return userDoctorId == appointment.DoctorId;

                case "Patient":
                    var userPatientId = GetCurrentUserPatientId(context);
                    return userPatientId == appointment.PatientId;

                default:
                    return false;
            }
        }
    }
}