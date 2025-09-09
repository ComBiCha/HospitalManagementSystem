namespace HospitalManagementSystem.Infrastructure.RabbitMQ
{
    public interface IRabbitMQService
    {
        Task PublishAppointmentCreatedAsync(object appointmentData);
        Task PublishAppointmentUpdatedAsync(object appointmentData);
        Task PublishAppointmentCancelledAsync(object appointmentData);
    }
}
