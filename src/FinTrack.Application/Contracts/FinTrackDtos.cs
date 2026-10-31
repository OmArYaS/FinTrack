namespace FinTrack.Application.Contracts;

// Requests -------------------------------------------------------------------

public sealed record OpenAccountRequest(string CustomerExternalId, string AccountType, string Currency);

public sealed record CreateCustomerRequest(string ExternalId, string FullName, string Email);

public sealed record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string? Description);

// Responses ------------------------------------------------------------------

public sealed record CustomerDto(Guid Id, string FullName, string Email, string ExternalId);

public sealed record AccountSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Number,
    string AccountType,
    string Status,
    string Currency,
    decimal Balance,
    DateTime OpenedAt);

public sealed record LedgerEntryDto(
    Guid TransactionId,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string Reference,
    string? Description,
    DateTime CreatedAt);

public sealed record TransferResultDto(string TransferReference, decimal FromBalanceAfter, decimal ToBalanceAfter);
