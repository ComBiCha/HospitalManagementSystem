using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HospitalManagementSystem.Application.Services;
using HospitalManagementSystem.Application.DTOs;

namespace HospitalManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Accountant")]
    public class AccountantController : ControllerBase
    {
        private readonly AccountantApplicationService _accountantService;
        private readonly ILogger<AccountantController> _logger;

        public AccountantController(AccountantApplicationService accountantService, ILogger<AccountantController> logger)
        {
            _accountantService = accountantService;
            _logger = logger;
        }

        [HttpGet("unpaid-medical-records")]
        public async Task<ActionResult<IEnumerable<UnpaidMedicalRecordDto>>> GetUnpaidMedicalRecords([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var records = await _accountantService.GetUnpaidMedicalRecordsAsync(page, pageSize);
                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unpaid medical records.");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("medical-records/{medicalRecordId}/payments/cash/initiate")]
        public async Task<ActionResult<PaymentDto>> InitiateCashPayment(int medicalRecordId)
        {
            try
            {
                var paymentDto = await _accountantService.InitiateCashPaymentForMedicalRecordAsync(medicalRecordId);
                return Ok(paymentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating cash payment for medical record {MedicalRecordId}", medicalRecordId);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("medical-records/{medicalRecordId}/payments/stripe/initiate")]
        public async Task<ActionResult<InitiateStripePaymentResponseDto>> InitiateStripePayment(int medicalRecordId)
        {
            try
            {
                var responseDto = await _accountantService.InitiateStripePaymentForMedicalRecordAsync(medicalRecordId);
                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating Stripe payment for medical record {MedicalRecordId}", medicalRecordId);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("payments/{paymentId}/cash/confirm")]
        public async Task<IActionResult> ConfirmCashPayment(int paymentId)
        {
            try
            {
                var success = await _accountantService.ConfirmCashPaymentAsync(paymentId);
                if (!success)
                {
                    return BadRequest("Could not confirm payment. Payment may not be pending, not a cash payment, or the medical record is already paid.");
                }
                return Ok(new { message = "Payment confirmed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming cash payment for payment {PaymentId}", paymentId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("payments/{paymentId}/cash/cancel")]
        public async Task<IActionResult> CancelCashPayment(int paymentId)
        {
            try
            {
                var success = await _accountantService.CancelCashPaymentAsync(paymentId);
                if (!success)
                {
                    return BadRequest("Could not cancel payment. Payment may not be pending or not a cash payment.");
                }
                return Ok(new { message = "Payment cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling cash payment for payment {PaymentId}", paymentId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("payments/{paymentId}/stripe/cancel")]
        public async Task<IActionResult> CancelStripePayment(int paymentId)
        {
            try
            {
                var success = await _accountantService.CancelStripePaymentAsync(paymentId);
                if (!success)
                {
                    return BadRequest("Could not cancel Stripe payment. Payment may not be a pending Stripe payment.");
                }
                return Ok(new { message = "Stripe payment cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling stripe payment for payment {PaymentId}", paymentId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
