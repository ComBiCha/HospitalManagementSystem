using PatientService.API.Repositories;

namespace PatientService.API.Services
{
    public class PatientCacheWarmupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PatientCacheWarmupService> _logger;
        private readonly TimeSpan _warmupInterval = TimeSpan.FromHours(6);

        public PatientCacheWarmupService(IServiceProvider serviceProvider, ILogger<PatientCacheWarmupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Patient cache warmup service started");

            // Initial warmup delay
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            // Initial warmup
            await PerformWarmup();

            // Periodic warmup
            using var timer = new PeriodicTimer(_warmupInterval);
            
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PerformWarmup();
            }
        }

        private async Task PerformWarmup()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPatientRepository>();

                if (repository is CachedPatientRepository cachedRepo)
                {
                    _logger.LogInformation("Starting patient cache warmup...");
                    await cachedRepo.WarmupCacheAsync();
                    _logger.LogInformation("Patient cache warmup completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during patient cache warmup");
            }
        }
    }
}