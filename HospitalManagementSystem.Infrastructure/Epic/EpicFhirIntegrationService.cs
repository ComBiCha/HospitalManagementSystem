using HospitalManagementSystem.Domain.Fhir;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;


namespace HospitalManagementSystem.Infrastructure.Epic
{
    public class EpicFhirIntegrationService : IEhrFhirIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<EpicFhirIntegrationService> _logger; // Declared logger
        private readonly string _clientId;
        private readonly string _tokenUrl;
        private readonly string _privateKeyPath;

        public EpicFhirIntegrationService(HttpClient httpClient, IConfiguration config, ILogger<EpicFhirIntegrationService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger; // Assign logger
            var epicConfig = _config.GetSection("EpicFhir");
            _clientId = epicConfig["ClientId"] ?? throw new ArgumentNullException("EpicFhir:ClientId missing");
            _tokenUrl = epicConfig["TokenUrl"] ?? throw new ArgumentNullException("EpicFhir:TokenUrlUrl missing");
            _privateKeyPath = epicConfig["PrivateKeyPath"] ?? throw new ArgumentNullException("EpicFhir:PrivateKeyPath missing");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // Đọc private key PEM
            var pem = await File.ReadAllTextAsync(_privateKeyPath);

            // Parse PEM để lấy RSA key
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(
                source: Convert.FromBase64String(
                    Regex.Replace(pem, @"-----.*?-----|\s+", string.Empty, RegexOptions.Singleline)
                ),
                bytesRead: out _
            );

            var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha384);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = now + 300;
            var jti = Guid.NewGuid().ToString();

            var payload = new JwtPayload
            {
                { "iss", _clientId },
                { "sub", _clientId },
                { "aud", _tokenUrl },
                { "jti", jti },
                { "exp", exp },
                { "nbf", now },
                { "iat", now }
            };

            var header = new JwtHeader(credentials);
            var jwt = new JwtSecurityToken(header, payload);

            var handler = new JwtSecurityTokenHandler();
            var assertion = handler.WriteToken(jwt);

            // Gửi request lấy token
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                new KeyValuePair<string, string>("client_assertion", assertion)
            });

            using var tokenClient = new HttpClient();
            var response = await tokenClient.PostAsync(_tokenUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Epic token request failed: {response.StatusCode} - {responseBody}");
                throw new Exception($"Epic token request failed: {response.StatusCode} - {responseBody}");
            }
            using var doc = JsonDocument.Parse(responseBody);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            return accessToken!;
        }

        private async Task EnsureAccessTokenAsync()
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string?> GetPatientDemographicsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync($"Patient/{patientId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Patient with ID {PatientId} not found in Epic.", patientId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Epic FHIR request for Patient/{PatientId} failed with status {StatusCode}: {Error}", patientId, response.StatusCode, error);
                throw new Exception($"Epic FHIR request failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> SearchPatientsAsync (string? name = null,
                                                        string? email = null,
                                                        string? phone = null,
                                                        string? gender = null,
                                                        string? identifier = null)
        {
            await EnsureAccessTokenAsync();
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(name))
                queryParams.Add($"name={Uri.EscapeDataString(name)}");
            if (!string.IsNullOrEmpty(email))
                queryParams.Add($"email={Uri.EscapeDataString(email)}");
            if (!string.IsNullOrEmpty(phone))
                queryParams.Add($"phone={Uri.EscapeDataString(phone)}");
            if (!string.IsNullOrEmpty(gender))
                queryParams.Add($"gender={Uri.EscapeDataString(gender)}");
            if (!string.IsNullOrEmpty(identifier))
                queryParams.Add($"identifier={Uri.EscapeDataString(identifier)}");

            var queryString = string.Join("&", queryParams);
            var url = "Patient";
            if (queryParams.Count > 0)
                url += "?" + queryString;

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Epic search patients request failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<(bool, string)> VerifyPatientExistsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync($"Patient/{patientId}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var nameArray = doc.RootElement.GetProperty("name")[0];
                var familyName = nameArray.GetProperty("family").GetString();
                var givenName = nameArray.GetProperty("given")[0].GetString();
                return (true, $"{givenName} {familyName}");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, null);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error verifying patient from Epic: {response.StatusCode} - {errorContent}");
        }

        private async Task<string> GetFhirResourceByPatientAsync(string resourceType, string patientId, string? query = null)
        {
            await EnsureAccessTokenAsync();
            var requestUri = $"{resourceType}?patient={patientId}";
            if (!string.IsNullOrEmpty(query))
            {
                requestUri += $"&{query}";
            }
            var response = await _httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("FHIR resource {ResourceType} for patient {PatientId} not found (404). Returning empty array.", resourceType, patientId);
                    return "[]"; // Return empty JSON array for 404s
                }
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Epic FHIR request for {resourceType} failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        public Task<string> GetMedicationRequestsAsync(string patientId)
        {
            return GetFhirResourceByPatientAsync("MedicationRequest", patientId);
        }

        public Task<string> GetMedicationStatementsAsync(string patientId)
        {
            return GetFhirResourceByPatientAsync("MedicationStatement", patientId);
        }

        public Task<string> GetAllergyIntolerancesAsync(string patientId)
        {
            return GetFhirResourceByPatientAsync("AllergyIntolerance", patientId);
        }

        public Task<string> GetConditionsAsync(string patientId)
        {
            return GetFhirResourceByPatientAsync("Condition", patientId);
        }

        public Task<string> GetObservationsAsync(string patientId, string? category = null)
        {
            string? query = null;
            if (!string.IsNullOrEmpty(category))
            {
                query = $"category={category}";
            }
            return GetFhirResourceByPatientAsync("Observation", patientId, query);
        }
    }
}