namespace FinTrack.Application.Accounts;

/// <summary>Configurable retail fraud-control limits applied by <see cref="TransferService"/>.</summary>
public sealed record TransferLimits
{
    public decimal MaxAmountPerTransaction { get; set; } = 25_000m;

    public decimal MaxDailyOutflowPerAccount { get; set; } = 100_000m;
}
