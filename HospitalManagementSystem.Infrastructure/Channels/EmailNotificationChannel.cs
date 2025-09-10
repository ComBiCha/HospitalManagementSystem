using System.Net.Mail;
using System.Net;
using HospitalManagementSystem.Domain.Notifications;

namespace HospitalManagementSystem.Infrastructure.Channels
{
    public class EmailNotificationChannel : INotificationChannel
    {
        public string ChannelType => "Email";

        public async Task<bool> SendAsync(NotificationMessage message)
        {
            var smtpServer = "smtp.gmail.com";
            var smtpPort = 587;
            var username = "sangrk2004@gmail.com";
            var password = "elhl wqmk qnva rwbj";

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mail = new MailMessage(username, message.Recipient, message.Subject, message.Content);

            try
            {
                await client.SendMailAsync(mail);
                Console.WriteLine($"Email sent to {message.Recipient}: {message.Subject}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                return false;
            }
        }

        public bool IsAvailable() => true;
    }
}