using FinTrack.Api.Authorization;
using FinTrack.Application.Accounts;
using FinTrack.Application.Common.Models;
using FinTrack.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

/// <summary>Paged ledger for one account; access is ownership- or staff-checked.</summary>
[ApiController]
[Route("api/accounts/{accountId:guid}/transactions")]
[Authorize]
[Produces("application/json")]
public sealed class TransactionsController(
    IAccountService accounts,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<LedgerEntryDto>>> List(
        Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(accountId, ct);
        var decision = await authorization.AuthorizeAsync(User, account.CustomerId.ToString(), Policies.CustomerOwnerOrStaff);
        if (!decision.Succeeded)
        {
            return Forbid();
        }

        return Ok(await accounts.GetLedgerAsync(accountId, page, pageSize, ct));
    }
}
