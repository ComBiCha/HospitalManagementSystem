using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("/")]
    public class HealthController : ControllerBase
    {
        private static bool _isKilled = false;
        private readonly IConfiguration _config;

        public HealthController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("liveness")]
        public IActionResult Liveness()
        {
            return Ok("Alive");
        }

        [HttpGet("readiness")]
        public IActionResult Readiness()
        {
            // Kiểm tra kết nối database
            var connStr = _config.GetConnectionString("DefaultConnection");
            try
            {
                using var conn = new NpgsqlConnection(connStr);
                conn.Open();
                using var cmd = new NpgsqlCommand("SELECT 1", conn);
                cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                return StatusCode(503, $"DB not ready: {ex.Message}");
            }

            return Ok("Ready");
        }

        [HttpGet("healthz")]
        public IActionResult Healthz()
        {
            return Ok("Healthy");
        }

        [HttpGet("kill")]
        public IActionResult Kill()
        {
            Environment.Exit(1);
            return StatusCode(500, "App killed");
        }
    }
}