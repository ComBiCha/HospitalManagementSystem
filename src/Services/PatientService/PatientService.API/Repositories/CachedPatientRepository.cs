using PatientService.API.Models;
using PatientService.API.Services.Caching;

namespace PatientService.API.Repositories
{
    public class CachedPatientRepository : IPatientRepository
    {
        private readonly IPatientRepository _repository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CachedPatientRepository> _logger;

        public CachedPatientRepository(
            IPatientRepository repository,
            ICacheService cacheService,
            ILogger<CachedPatientRepository> logger)
        {
            _repository = repository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<Patient?> GetPatientByIdAsync(int id)
        {
            var cacheKey = CacheKeyGenerator.PatientById(id);
            
            // Try to get from cache first
            var cachedPatient = await _cacheService.GetAsync<Patient>(cacheKey);
            if (cachedPatient != null)
            {
                _logger.LogDebug("Patient {PatientId} retrieved from cache", id);
                return cachedPatient;
            }

            // If not in cache, get from database
            var patient = await _repository.GetPatientByIdAsync(id);
            if (patient != null)
            {
                // Cache the result
                await _cacheService.SetAsync(cacheKey, patient, CacheExpiry.PatientInfo);
                _logger.LogDebug("Patient {PatientId} cached for {Expiry}", id, CacheExpiry.PatientInfo);
            }

            return patient;
        }

        public async Task<Patient?> GetPatientByEmailAsync(string email)
        {
            var cacheKey = CacheKeyGenerator.PatientByEmail(email);
            
            var cachedPatient = await _cacheService.GetAsync<Patient>(cacheKey);
            if (cachedPatient != null)
            {
                _logger.LogDebug("Patient with email {Email} retrieved from cache", email);
                return cachedPatient;
            }

            var patient = await _repository.GetPatientByEmailAsync(email);
            if (patient != null)
            {
                await _cacheService.SetAsync(cacheKey, patient, CacheExpiry.PatientInfo);
                
                // Also cache by ID for cross-reference
                var idCacheKey = CacheKeyGenerator.PatientById(patient.Id);
                await _cacheService.SetAsync(idCacheKey, patient, CacheExpiry.PatientInfo);
                
                _logger.LogDebug("Patient with email {Email} cached", email);
            }

            return patient;
        }

        public async Task<IEnumerable<Patient>> GetAllPatientsAsync(int page = 1, int pageSize = 50)
        {
            var cacheKey = CacheKeyGenerator.AllPatients(page, pageSize);

            var cachedPatients = await _cacheService.GetAsync<IEnumerable<Patient>>(cacheKey);
            if (cachedPatients != null)
            {
                _logger.LogDebug("Patients page {Page} retrieved from cache", page);
                return cachedPatients;
            }

            var patients = await _repository.GetAllPatientsAsync(page, pageSize);
            if (patients?.Any() == true)
            {
                await _cacheService.SetAsync(cacheKey, patients, CacheExpiry.PatientList);
                _logger.LogDebug("Patients page {Page} cached ({Count} patients)", page, patients.Count());
            }

            return patients ?? Enumerable.Empty<Patient>();
        }

        public async Task<IEnumerable<Patient>> GetPatientsByNameAsync(string name)
        {
            var cacheKey = CacheKeyGenerator.PatientsByName(name);
            
            var cachedPatients = await _cacheService.GetAsync<IEnumerable<Patient>>(cacheKey);
            if (cachedPatients != null)
            {
                _logger.LogDebug("Patients with name {Name} retrieved from cache", name);
                return cachedPatients;
            }

            var patients = await _repository.GetPatientsByNameAsync(name);
            if (patients?.Any() == true)
            {
                await _cacheService.SetAsync(cacheKey, patients, CacheExpiry.PatientSearch);
                _logger.LogDebug("Patients with name {Name} cached", name);
            }

            return patients ?? Enumerable.Empty<Patient>();
        }

        public async Task<Patient> CreatePatientAsync(Patient patient)
        {
            var createdPatient = await _repository.CreatePatientAsync(patient);
            
            // Cache the new patient
            var idCacheKey = CacheKeyGenerator.PatientById(createdPatient.Id);
            var emailCacheKey = CacheKeyGenerator.PatientByEmail(createdPatient.Email);
            
            await _cacheService.SetAsync(idCacheKey, createdPatient, CacheExpiry.PatientInfo);
            await _cacheService.SetAsync(emailCacheKey, createdPatient, CacheExpiry.PatientInfo);
            
            // Invalidate list caches since we added a new patient
            await InvalidateListCaches();
            
            _logger.LogInformation("Patient {PatientId} created and cached", createdPatient.Id);
            return createdPatient;
        }

        public async Task<Patient?> UpdatePatientAsync(Patient patient)
        {
            var existingPatient = await _repository.GetPatientByIdAsync(patient.Id);
            var updatedPatient = await _repository.UpdatePatientAsync(patient);

            if (updatedPatient != null)
            {
                // Nếu dữ liệu không đổi, không cache lại
                if (existingPatient != null &&
                    existingPatient.Name == updatedPatient.Name &&
                    existingPatient.Email == updatedPatient.Email &&
                    existingPatient.Age == updatedPatient.Age &&
                    existingPatient.Status == updatedPatient.Status)
                {
                    return updatedPatient;
                }

                var idCacheKey = CacheKeyGenerator.PatientById(patient.Id);
                var emailCacheKey = CacheKeyGenerator.PatientByEmail(updatedPatient.Email);

                // Nếu email đổi, xóa cache cũ theo email
                if (existingPatient != null && existingPatient.Email != updatedPatient.Email)
                {
                    var oldEmailCacheKey = CacheKeyGenerator.PatientByEmail(existingPatient.Email);
                    await _cacheService.RemoveAsync(oldEmailCacheKey);
                }

                // Cache lại theo id và email (dù email đổi hay không)
                await _cacheService.SetAsync(idCacheKey, updatedPatient, CacheExpiry.PatientInfo);
                await _cacheService.SetAsync(emailCacheKey, updatedPatient, CacheExpiry.PatientInfo);

                // Invalidate các cache liên quan
                await InvalidateListCaches();
                await InvalidateSearchCaches(updatedPatient.Name);
                if (existingPatient?.Name != updatedPatient.Name)
                {
                    await InvalidateSearchCaches(existingPatient?.Name);
                }

                _logger.LogInformation("Patient {PatientId} updated and cache refreshed", patient.Id);
            }

            return updatedPatient;
        }

        public async Task<bool> DeletePatientAsync(int id)
        {
            // Get patient first to know details for cache invalidation
            var patient = await _repository.GetPatientByIdAsync(id);
            
            var result = await _repository.DeletePatientAsync(id);
            
            if (result && patient != null)
            {
                // Remove from caches
                var idCacheKey = CacheKeyGenerator.PatientById(id);
                var emailCacheKey = CacheKeyGenerator.PatientByEmail(patient.Email);
                
                await _cacheService.RemoveAsync(idCacheKey);
                await _cacheService.RemoveAsync(emailCacheKey);
                
                // Invalidate related caches
                await InvalidateListCaches();
                await InvalidateSearchCaches(patient.Name);
                
                _logger.LogInformation("Patient {PatientId} deleted and removed from cache", id);
            }

            return result;
        }

        public async Task<IEnumerable<Patient>> GetRecentPatientsAsync(int count = 50)
        {
            var cacheKey = CacheKeyGenerator.RecentPatients(count);
            
            var cachedPatients = await _cacheService.GetAsync<IEnumerable<Patient>>(cacheKey);
            if (cachedPatients != null)
            {
                _logger.LogDebug("Recent {Count} patients retrieved from cache", count);
                return cachedPatients;
            }

            var recentPatients = await _repository.GetRecentPatientsAsync(count);

            if (recentPatients.Any())
            {
                await _cacheService.SetAsync(cacheKey, recentPatients, CacheExpiry.PatientList);
                _logger.LogDebug("Recent {Count} patients cached", count);
            }

            return recentPatients;
        }

        public async Task<int> GetPatientCountAsync()
        {
            var cacheKey = CacheKeyGenerator.PatientCount();
            
            var cachedCount = await _cacheService.GetStringAsync(cacheKey);
            if (cachedCount != null && int.TryParse(cachedCount, out var count))
            {
                _logger.LogDebug("Patient count retrieved from cache: {Count}", count);
                return count;
            }

            var patientCount = await _repository.GetPatientCountAsync();

            await _cacheService.SetStringAsync(cacheKey, patientCount.ToString(), CacheExpiry.PatientStatistics);
            _logger.LogDebug("Patient count cached: {Count}", patientCount);

            return patientCount;
        }

        private async Task InvalidateListCaches()
        {
            var listKeys = new[]
            {
                CacheKeyGenerator.AllPatients(1, 50),
                CacheKeyGenerator.PatientCount()
            };

            foreach (var key in listKeys)
            {
                await _cacheService.RemoveAsync(key);
            }

            // Also invalidate recent patients caches
            await _cacheService.RemovePatternAsync("patient:recent:*");
            
            _logger.LogDebug("List caches invalidated");
        }

        private async Task InvalidateSearchCaches(string? name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var searchKey = CacheKeyGenerator.PatientsByName(name);
                await _cacheService.RemoveAsync(searchKey);
                _logger.LogDebug("Search cache invalidated for name: {Name}", name);
            }
        }

        // Cache management methods
        public async Task WarmupCacheAsync()
        {
            _logger.LogInformation("Starting patient cache warmup...");
            
            try
            {
                // Cache recent patients
                await GetRecentPatientsAsync(100);
                
                // Cache patient count
                await GetPatientCountAsync();
                
                _logger.LogInformation("Patient cache warmup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during patient cache warmup");
            }
        }

        public async Task ClearAllCacheAsync()
        {
            try
            {
                await _cacheService.RemovePatternAsync(CacheKeyGenerator.AllPatientsPattern());
                _logger.LogInformation("All patient caches cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing patient caches");
            }
        }
    }
}