namespace HospitalManagementSystem.Application.DTOs.AITools;

public class BookAppointmentResponseDTO
{
    public int AppointmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
}
