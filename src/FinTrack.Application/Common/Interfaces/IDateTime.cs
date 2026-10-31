using System;

namespace FinTrack.Application.Common.Interfaces;

/// <summary>Clock abstraction so use cases are fully unit-testable. All values are UTC.</summary>
public interface IDateTime
{
    DateTime Now { get; }
}
