migration_name="auto_$(date +%Y%m%d%H%M%S)"
dotnet ef migrations add $migration_name --project HospitalManagementSystem.Infrastructure --startup-project HospitalManagementSystem.API
dotnet run --project HospitalManagementSystem.API