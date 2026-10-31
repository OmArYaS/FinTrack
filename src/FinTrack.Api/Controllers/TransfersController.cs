using FinTrack.Application.Contracts;
using FinTrack.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

/// <summary>Web API endpoint for money movement. Business rules live in the Application layer.</summary>
[ApiController]
[Route("api/transfers")]
[Authorize]
[Produces("application/json")]
public sealed class TransfersController(ITransferService transfers) : ControllerBase
{
    /// <summary>Posts an internal transfer between two accounts.</summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/transfers
    ///     {
    ///       "fromAccountId": "<guid>",
    ///       "toAccountId": "<guid>",
    ///       "amount": 250.00,
    ///       "description": "August rent"
    ///     }
    ///
    /// Business codes returned via ProblemDetails: SELF_TRANSFER, INVALID_AMOUNT,
    /// CURRENCY_MISMATCH, EXCEEDS_DAILY_LIMIT, INSUFFICIENT_FUNDS, ACCOUNT_CLOSED,
    /// ACCOUNT_FROZEN, CONCURRENT_UPDATE.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<TransferResultDto>> Transfer(
        [FromBody] TransferRequest request, CancellationToken ct)
    {
        var result = await transfers.ExecuteAsync(request, ct);
        return Ok(result);
    }
}
