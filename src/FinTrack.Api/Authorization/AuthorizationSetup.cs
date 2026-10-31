using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace FinTrack.Api.Authorization;

public static class Policies
{
    /// <summary>Back-office staff: Teller or Admin.</summary>
    public const string Staff = "Staff";

    /// <summary>Resource-based: caller must own the customer record or be staff.</summary>
    public const string CustomerOwnerOrStaff = "CustomerOwnerOrStaff";
}

public sealed class SameCustomerOrStaffRequirement : IAuthorizationRequirement;

/// <summary>
/// Resource = customer id (string). Customer callers pass only when the route resource
/// equals their identity-provider subject claim (oid); staff passes always.
/// </summary>
public sealed class SameCustomerOrStaffHandler : AuthorizationHandler<SameCustomerOrStaffRequirement, string>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameCustomerOrStaffRequirement requirement,
        string resource)
    {
        if (context.User.IsInRole("Teller") || context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var callerId = context.User.FindFirstValue("oid")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(callerId) &&
            string.Equals(callerId, resource, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class AuthorizationSetup
{
    public static IServiceCollection AddFinTrackAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Staff, policy => policy.RequireRole("Teller", "Admin"));
            options.AddPolicy(Policies.CustomerOwnerOrStaff, policy =>
                policy.Requirements.Add(new SameCustomerOrStaffRequirement()));
        });

        services.AddSingleton<IAuthorizationHandler, SameCustomerOrStaffHandler>();
        return services;
    }
}
