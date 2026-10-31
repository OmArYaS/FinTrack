using FinTrack.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Application;

public static class DependencyInjection
{
    /// <summary>Registers the business layer. NOTE: no infrastructure knowledge leaks in here.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransferService, TransferService>();
        return services;
    }
}
