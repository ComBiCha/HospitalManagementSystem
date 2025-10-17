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

        #region Eligible Appointments for Deposit

        [HttpGet("eligible-for-deposit-appointments")]
        public async Task<ActionResult<PaginatedResultDto<EligibleAppointmentDto>>> GetEligibleForDepositAppointments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _accountantService.GetEligibleForDepositAppointmentsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting eligible for deposit appointments.");
                return StatusCode(500, "Internal server error");
            }
        }

        #endregion

        #region Advance Payments

        [HttpGet("appointments/{appointmentId}/advance-payment-suggestion")]
        public async Task<ActionResult<decimal>> GetAdvancePaymentSuggestion(int appointmentId)
        {
            try
            {
                var amount = await _accountantService.GetAdvancePaymentSuggestionAsync(appointmentId);
                return Ok(new { suggestedAmount = amount });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Could not find suggestion for appointment {AppointmentId}", appointmentId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting advance payment suggestion for appointment {AppointmentId}", appointmentId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("appointments/{appointmentId}/advance-payments/cash/initiate")]
        public async Task<ActionResult<PaymentDto>> InitiateAdvanceCashPayment(int appointmentId)
        {
            try
            {
                var paymentDto = await _accountantService.InitiateAdvanceCashPaymentAsync(appointmentId);
                return Ok(paymentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating advance cash payment for appointment {AppointmentId}", appointmentId);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("appointments/{appointmentId}/advance-payments/stripe/initiate")]
        public async Task<ActionResult<InitiateStripePaymentResponseDto>> InitiateAdvanceStripePayment(int appointmentId)
        {
            try
            {
                var responseDto = await _accountantService.InitiateAdvanceStripePaymentAsync(appointmentId);
                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating advance Stripe payment for appointment {AppointmentId}", appointmentId);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        #endregion

        #region Final Payments

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

        // [HttpPost("medical-records/{medicalRecordId}/payments/stripe/initiate")]
        // public async Task<ActionResult<InitiateStripePaymentResponseDto>> InitiateStripePayment(int medicalRecordId)
        // {
        //     try
        //     {
        //         var responseDto = await _accountantService.InitiateStripePaymentForMedicalRecordAsync(medicalRecordId);
        //         return Ok(responseDto);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error initiating Stripe payment for medical record {MedicalRecordId}", medicalRecordId);
        //         return StatusCode(500, "Internal server error: " + ex.Message);
        //     }
        // }

        #endregion

        #region Payment Management

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

        #endregion

        #region Refund Management

        [HttpGet("refundable-medical-records")]
        public async Task<ActionResult<IEnumerable<RefundableMedicalRecordDto>>> GetRefundableMedicalRecords([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var records = await _accountantService.GetRefundableMedicalRecordsAsync(page, pageSize);
            return Ok(records);
        }

        [HttpPost("medical-records/{medicalRecordId}/initiate-refund")]
        public async Task<ActionResult<PaymentDto>> InitiateRefundPayment(int medicalRecordId)
        {
            try
            {
                var paymentDto = await _accountantService.InitiateRefundPaymentAsync(medicalRecordId);
                return Ok(paymentDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating refund for medical record {MedicalRecordId}", medicalRecordId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("payments/{paymentId}/complete-refund")]
        public async Task<IActionResult> CompleteRefundPayment(int paymentId)
        {
            try
            {
                var success = await _accountantService.CompleteRefundPaymentAsync(paymentId);
                if (!success)
                {
                    return BadRequest("Could not complete refund. Payment may not be a pending refund.");
                }
                return Ok(new { message = "Refund completed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing refund for payment {PaymentId}", paymentId);
                return StatusCode(500, "Internal server error");
            }
        }

        #endregion
    }
}
