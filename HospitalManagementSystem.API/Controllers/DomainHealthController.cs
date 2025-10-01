using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DomainHealthController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Check([FromQuery] string domain, [FromQuery] string subnet)
        {
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(subnet))
                return BadRequest("Missing domain or subnet");

            // 1. Kiểm tra DNS
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(domain);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"DNS FAIL: {ex.Message}");
            }

            var ipList = addresses.Select(a => a.ToString()).ToList();
            var found = ipList.Any(ip => ip.StartsWith(subnet));
            if (!found)
            {
                return StatusCode(500, $"DNS FAIL: {domain} trả về {string.Join(", ", ipList)}, không thuộc subnet {subnet}*");
            }

            // 2. Kiểm tra HTTP
            var httpClient = new HttpClient();
            HttpResponseMessage httpResp;
            try
            {
                httpResp = await httpClient.GetAsync($"http://{domain}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"HTTP FAIL: {ex.Message}");
            }
            if ((int)httpResp.StatusCode != 200)
            {
                return StatusCode(500, $"HTTP FAIL: Trang chủ trả về {(int)httpResp.StatusCode}");
            }

            // 3. Kiểm tra HTTPS và chứng chỉ SSL
            HttpResponseMessage httpsResp;
            try
            {
                httpsResp = await httpClient.GetAsync($"https://{domain}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"SSL FAIL: {ex.Message}");
            }
            if ((int)httpsResp.StatusCode != 200)
            {
                return StatusCode(500, $"SSL FAIL: HTTPS trả về {(int)httpsResp.StatusCode}");
            }

            // 4. Kiểm tra chứng chỉ hết hạn
            try
            {
                var req = (HttpWebRequest)WebRequest.Create($"https://{domain}");
                req.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) =>
                {
                    var x509 = new X509Certificate2(cert);
                    var notAfter = x509.NotAfter;
                    var issuer = x509.Issuer;
                    if (DateTime.UtcNow > notAfter)
                    {
                        throw new Exception($"SSL FAIL: Chứng chỉ đã hết hạn vào {notAfter}");
                    }
                    return true;
                };
                using var resp = (HttpWebResponse)req.GetResponse();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

            // 5. Kiểm tra latency
            var sw = Stopwatch.StartNew();
            await httpClient.GetAsync($"http://{domain}");
            sw.Stop();
            var latency = sw.Elapsed.TotalSeconds;

            return Ok(new
            {
                DNS = $"IP trả về: {string.Join(", ", ipList)}",
                HTTP = "HTTP OK",
                SSL = "SSL OK",
                Latency = $"{latency} giây"
            });
        }
    }
}