using FinTrack.Application.Common.Interfaces;

namespace FinTrack.Infrastructure.Common;

public sealed class DateTimeProvider : IDateTime
{
    public DateTime Now => DateTime.UtcNow;
}

/// <summary>Fixed clock for deterministic tests / demos.</summary>
public sealed class FixedDateTime(DateTime fixedTime) : IDateTime
{
    public DateTime Now => fixedTime;
}
