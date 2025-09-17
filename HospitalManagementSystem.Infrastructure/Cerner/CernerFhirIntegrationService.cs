using HospitalManagementSystem.Domain.Fhir;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HospitalManagementSystem.Infrastructure.Cerner
{
    public class CernerFhirIntegrationService : IEhrFhirIntegrationService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private string? _accessToken;

        public CernerFhirIntegrationService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken)) return;

            var cernerConfig = _config.GetSection("CernerFhir");
            var clientId = cernerConfig["ClientId"];
            var clientSecret = cernerConfig["ClientSecret"];
            var tokenUrl = cernerConfig["TokenUrl"];

            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            var body = $"grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}&scope=system/Patient.read";
            request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Cerner token request failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        }

        public async Task<string> GetPatientDemographicsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();

            var cernerConfig = _config.GetSection("CernerFhir");
            var fhirBaseUrl = cernerConfig["FhirBaseUrl"];
            var url = $"{fhirBaseUrl}Patient/{patientId}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Cerner FHIR request failed: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadAsStringAsync(); // JSON FHIR Patient
        }
        public async Task<string> SearchPatientsAsync (string? name = null, string? email = null, string? phone = null, string? gender = null, string? identifier = null)
        {
            await EnsureAccessTokenAsync();
            return await Task.FromResult(string.Empty);
        }
    }
}