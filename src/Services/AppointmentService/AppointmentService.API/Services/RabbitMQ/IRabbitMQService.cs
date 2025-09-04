namespace AppointmentService.API.Services.RabbitMQ
{
    public interface IRabbitMQService
    {
        Task PublishAppointmentCreatedAsync(object appointmentData);
        Task PublishAppointmentUpdatedAsync(object appointmentData);
        Task PublishAppointmentCancelledAsync(object appointmentData);
    }
}
