# Hospital Management System - Microservices Architecture

A comprehensive Hospital Management System built with microservices architecture using .NET 9.0 and PostgreSQL.

## 🏗️ Architecture

This project demonstrates microservices architecture with the following services:

- **Patient Service** - Manage patient information and profiles
- **Doctor Service** - Manage doctor profiles and specializations  
- **Appointment Service** - Handle appointment scheduling and management
- **Billing Service** - Process payments and manage billing with Stripe integration
- **Image Service** - Handle medical image storage with MinIO object storage
- **Authentication Service** - JWT-based authentication and authorization
- **Notification Service** - Handle notifications and messaging

## 🛠️ Technology Stack

- **Backend**: C# .NET 9.0 Web API
- **Database**: PostgreSQL (Database per service pattern)
- **Object Storage**: MinIO for medical images
- **Message Queue**: RabbitMQ for async communication
- **Authentication**: JWT Bearer tokens
- **Payment Processing**: Stripe integration
- **Containerization**: Docker & Docker Compose
- **API Documentation**: Swagger/OpenAPI

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- Docker Desktop
- PostgreSQL (via Docker)
- Git

### Running with Docker Compose

1. Clone the repository:
```bash
git clone https://github.com/ComBiCha/HospitalManagementSystem.git
cd HospitalManagementSystem
```

2. Start all services:
```bash
docker compose up -d
```

3. Services will be available at:
   - **Patient Service**: http://localhost:5001
   - **Doctor Service**: http://localhost:5201  
   - **Appointment Service**: http://localhost:5301
   - **Billing Service**: http://localhost:7001
   - **Image Service**: http://localhost:7005
   - **Auth Service**: http://localhost:5501
   - **MinIO Console**: http://localhost:9001 (minioadmin/minioadmin)
   - **RabbitMQ Management**: http://localhost:15672 (guest/guest)

### Running Individual Services

Each service can be run independently:

```bash
# Navigate to specific service
cd src/Services/ImageService/ImageService.API

# Restore dependencies
dotnet restore

# Run the service
dotnet run
```

## 📋 API Documentation

Each service provides Swagger documentation:

- Image Service: http://localhost:7005/swagger
- Billing Service: http://localhost:7001/swagger
- Patient Service: http://localhost:5001/swagger

## 🔧 Configuration

### Database Configuration

Each service uses its own PostgreSQL database:

- **Patient DB**: Port 5432
- **Doctor DB**: Port 5433  
- **Appointment DB**: Port 5434
- **Notification DB**: Port 5435
- **Auth DB**: Port 5436
- **Billing DB**: Port 5437
- **Image DB**: Port 5438

### Environment Variables

Key configuration settings:

```bash
# JWT Configuration
JWT_SECRET_KEY=HMS_SuperSecretKey_ForDevelopment_2024_MustBe32CharsOrMore!
JWT_ISSUER=HMS.AuthService
JWT_AUDIENCE=HMS.Services

# Database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres123

# MinIO
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin

# RabbitMQ
RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=guest
```

## 🧪 Testing

### API Testing with Postman

Import the provided Postman collections for testing each service:

1. **Authentication**: Login and get JWT tokens
2. **Image Upload**: Upload medical images (Doctor role required)
3. **Image Download**: Download medical images (Patient/Doctor roles)
4. **Billing**: Process payments with Stripe
5. **Appointments**: Schedule and manage appointments

### Sample API Calls

#### Upload Medical Image
```bash
curl -X POST \
  http://localhost:7005/api/images/upload \
  -H 'Authorization: Bearer YOUR_JWT_TOKEN' \
  -F 'AppointmentId=1' \
  -F 'DoctorId=1' \
  -F 'PatientId=1' \
  -F 'Image=@medical-scan.jpg' \
  -F 'Description=X-ray scan' \
  -F 'ImageType=xray'
```

#### Process Payment
```bash
curl -X POST \
  http://localhost:7001/api/billing/process-payment \
  -H 'Authorization: Bearer YOUR_JWT_TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{
    "appointmentId": 1,
    "patientId": 1,
    "amount": 150.00,
    "currency": "USD",
    "paymentMethod": "Stripe"
  }'
```

## 🔐 Security

- **JWT Authentication**: All services require valid JWT tokens
- **Role-based Authorization**: Different endpoints require specific roles (Doctor, Patient, Admin)
- **CORS Configuration**: Properly configured for development and production
- **API Key Protection**: Stripe keys and sensitive data properly secured

## 📁 Project Structure

