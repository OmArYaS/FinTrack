using FinTrack.Api.Authorization;
using FinTrack.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinTrack.Infrastructure.Persistence;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
[Produces("application/json")]
public sealed class CustomersController(FinTrackDbContext db, IAuthorizationService authorization) : ControllerBase
{
    /// <summary>Customer directory (staff operation).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> List(CancellationToken ct)
    {
        var items = await db.Customers
            .OrderBy(c => c.FullName)
            .Select(c => new CustomerDto(c.Id, c.FullName, c.Email, c.ExternalId))
            .ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>Customer profile. Retail callers may only read their own record.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return NotFound();
        }

        var policy = await authorization.AuthorizeAsync(User, customer.Id.ToString(), Policies.CustomerOwnerOrStaff);
        if (!policy.Succeeded)
        {
            return Forbid();
        }

        return Ok(new CustomerDto(customer.Id, customer.FullName, customer.Email, customer.ExternalId));
    }

    /// <summary>Provisions a customer profile (staff). In production this follows the Azure AD B2C registration webhook.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = new FinTrack.Domain.Entities.Customer
        {
            Id = Guid.NewGuid(),
            ExternalId = request.ExternalId.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { code = "CUSTOMER_EXISTS", detail = "A customer with this id or email already exists." });
        }

        return Created($"/api/customers/{customer.Id}",
            new CustomerDto(customer.Id, customer.FullName, customer.Email, customer.ExternalId));
    }
}
