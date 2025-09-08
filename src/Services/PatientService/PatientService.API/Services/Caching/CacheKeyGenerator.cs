namespace PatientService.API.Services.Caching
{
    public static class CacheKeyGenerator
    {
        private const string SEPARATOR = ":";
        private const string PATIENT_PREFIX = "patient";
        
        // Patient Service Keys
        public static string PatientById(int patientId) => $"{PATIENT_PREFIX}{SEPARATOR}id{SEPARATOR}{patientId}";
        public static string PatientByEmail(string email) => $"{PATIENT_PREFIX}{SEPARATOR}email{SEPARATOR}{email.ToLowerInvariant()}";
        public static string PatientsByName(string name) => $"{PATIENT_PREFIX}{SEPARATOR}name{SEPARATOR}{name.ToLowerInvariant()}";
        public static string AllPatients(int page, int pageSize) => $"patient:all:{page}:{pageSize}";
        public static string RecentPatients(int count) => $"{PATIENT_PREFIX}{SEPARATOR}recent{SEPARATOR}{count}";
        
        // Patient Statistics
        public static string PatientCount() => $"{PATIENT_PREFIX}{SEPARATOR}count";
        
        // Patterns for bulk operations
        public static string PatientPattern(int? patientId = null) => 
            patientId.HasValue ? $"{PATIENT_PREFIX}{SEPARATOR}*{SEPARATOR}{patientId}" : $"{PATIENT_PREFIX}{SEPARATOR}*";
        
        public static string AllPatientsPattern() => $"{PATIENT_PREFIX}{SEPARATOR}*";
    }

    public static class CacheExpiry
    {
        public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);      // For frequently changing data
        public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);    // For moderately changing data
        public static readonly TimeSpan Long = TimeSpan.FromHours(2);         // For rarely changing data
        public static readonly TimeSpan VeryLong = TimeSpan.FromHours(24);    // For static data
        
        // Specific expiry times for Patient data
        public static readonly TimeSpan PatientInfo = Medium;           // 30 minutes
        public static readonly TimeSpan PatientList = Short;           // 5 minutes
        public static readonly TimeSpan PatientStatistics = Long;      // 2 hours
        public static readonly TimeSpan PatientSearch = Short;         // 5 minutes
    }
}