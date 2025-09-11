using System.Collections.Generic;

namespace HospitalManagementSystem.Infrastructure.Configuration
{
    public static class EnvKeyMapping
    {
        public static readonly Dictionary<string, string> Map = new()
        {
            // Database
            { "DB-CONN", "ConnectionStrings:DefaultConnection" },
            { "REDIS-CONN", "ConnectionStrings:Redis" },

            // Redis
            { "REDIS-PASSWORD", "Redis:Password" },

            // JWT
            { "JWT-SECRET", "Jwt:SecretKey" },
            { "JWT-ISSUER", "Jwt:Issuer" },
            { "JWT-AUDIENCE", "Jwt:Audience" },
            { "JWT-EXPIRY", "Jwt:ExpiryMinutes" },

            // Epic FHIR
            { "EPIC-BASEURL", "EpicFhir:BaseUrl" },
            { "EPIC-CLIENTID", "EpicFhir:ClientId" },
            { "EPIC-TOKENURL", "EpicFhir:TokenUrl" },
            { "EPIC-PRIVATEKEY", "EpicFhir:PrivateKeyPath" },

            // RabbitMQ
            { "RABBITMQ-HOST", "RabbitMQ:HostName" },
            { "RABBITMQ-PORT", "RabbitMQ:Port" },
            { "RABBITMQ-USER", "RabbitMQ:UserName" },
            { "RABBITMQ-PASS", "RabbitMQ:Password" },

            // Stripe
            { "STRIPE-PUB", "Stripe:PublishableKey" },
            { "STRIPE-SECRET", "Stripe:SecretKey" },

            // Email
            { "EMAIL-SMTP", "Email:SmtpServer" },
            { "EMAIL-PORT", "Email:SmtpPort" },
            { "EMAIL-USER", "Email:Username" },
            { "EMAIL-PASS", "Email:Password" },
            { "EMAIL-FROM", "Email:FromEmail" },
            { "EMAIL-NAME", "Email:FromName" },

            // SMS
            { "SMS-SID", "SMS:AccountSid" },
            { "SMS-TOKEN", "SMS:AuthToken" },
            { "SMS-FROM", "SMS:FromNumber" },

            // Push
            { "PUSH-SERVERKEY", "Push:ServerKey" },
            { "PUSH-SENDER", "Push:SenderId" },

            // Notification
            { "NOTI-RETRY", "NotificationService:RetryAttempts" },
            { "NOTI-DELAY", "NotificationService:RetryDelayMinutes" },
            { "NOTI-EMAIL", "NotificationService:EnableEmail" },
            { "NOTI-SMS", "NotificationService:EnableSMS" },
            { "NOTI-PUSH", "NotificationService:EnablePush" },

            // SeaweedFS
            { "SEAWEED-MASTER", "SeaweedFS:MasterUrl" },
            { "SEAWEED-PUBLIC", "SeaweedFS:PublicUrl" },
            { "SEAWEED-REPL", "SeaweedFS:Replication" },
            { "SEAWEED-COLL", "SeaweedFS:Collection" }
        };
    }
}
