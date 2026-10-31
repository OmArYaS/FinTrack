namespace FinTrack.Domain.Enums;

public enum AccountType
{
    Checking = 1,
    Savings = 2,
    FixedDeposit = 3
}

public enum AccountStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum TransactionType
{
    Opening = 1,
    Deposit = 2,
    Withdrawal = 3,
    TransferIn = 4,
    TransferOut = 5,
    Fee = 6
}
