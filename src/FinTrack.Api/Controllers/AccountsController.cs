using FinTrack.Api.Authorization;
using FinTrack.Application.Accounts;
using FinTrack.Application.Common.Exceptions;
using FinTrack.Application.Common.Interfaces;
using FinTrack.Application.Contracts;
using FinTrack.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
[Produces("application/json")]
public sealed class AccountsController(
    IAccountService accounts,
    IAuthorizationService authorization,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Lists accounts. Staff may omit customerId for a full overview; retail callers see their own.</summary>
    /// <remarks>GET /api/accounts?customerId=… — ownership is enforced inside the service (defense in depth).</remarks>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> List(
        [FromQuery(Name = "customerId")] Guid? customerId, CancellationToken ct) =>
        Ok(await accounts.GetForCallerAsync(currentUser, customerId, ct));

    /// <summary>Gets one account summary. Access requires ownership or staff role.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountSummaryDto>> Get(Guid id, CancellationToken ct)
    {
        var account = await accounts.GetAsync(id, ct);
        var decision = await authorization.AuthorizeAsync(User, account.CustomerId.ToString(), Policies.CustomerOwnerOrStaff);
        if (!decision.Succeeded)
        {
            return Forbid();
        }

        return Ok(account);
    }

    /// <summary>Opens a new account for a customer (staff operation).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<AccountSummaryDto>> Open([FromBody] OpenAccountRequest request, CancellationToken ct)
    {
        var created = await accounts.OpenAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Closes an account; must be zero-balanced first (staff operation).</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        await accounts.CloseAsync(id, ct);
        return NoContent();
    }
}
