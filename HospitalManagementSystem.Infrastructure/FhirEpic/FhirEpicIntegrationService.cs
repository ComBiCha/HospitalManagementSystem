using HospitalManagementSystem.Domain.FhirEpic;
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


namespace HospitalManagementSystem.Infrastructure.Services
{
    public class FhirEpicIntegrationService : IFhirEpicIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly string _clientId;
        private readonly string _tokenUrl;
        private readonly string _privateKeyPath;

        public FhirEpicIntegrationService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
            var epicConfig = _config.GetSection("EpicFhir");
            _clientId = epicConfig["ClientId"] ?? throw new ArgumentNullException("EpicFhir:ClientId missing");
            _tokenUrl = epicConfig["TokenUrl"] ?? throw new ArgumentNullException("EpicFhir:TokenUrl missing");
            _privateKeyPath = epicConfig["PrivateKeyPath"] ?? throw new ArgumentNullException("EpicFhir:PrivateKeyPath missing");

            var baseUrl = epicConfig["BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
                _httpClient.BaseAddress = new Uri(baseUrl);
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
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            return accessToken!;
        }

        private async Task EnsureAccessTokenAsync()
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string> GetPatientDemographicsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync($"Patient/{patientId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Epic token request failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetAppointmentsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync($"Appointment?patient={patientId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Epic token request failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetMedicationsAsync(string patientId)
        {
            await EnsureAccessTokenAsync();
            var response = await _httpClient.GetAsync($"MedicationRequest?patient={patientId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Epic token request failed: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadAsStringAsync();
        }
    }
}