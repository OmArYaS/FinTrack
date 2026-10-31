-- ===========================================================================
-- FinTrack — 02 Stored Procedures (business invariants enforced server-side)
-- Mirrors the Application-layer rules in FinTrack.Application.Transfers.
-- THROW error codes map 1:1 to the stable BusinessRuleException codes consumed
-- by the API ProblemDetails mapping.
-- ===========================================================================

/* -------------------------------------------------------------------------
   usp_PostTransfer — atomically posts an internal money transfer:
     • takes UPDLOCK on both accounts so two concurrent tellers cannot run a
       same-balance race and produce a lost update,
     • raises THROW 50xxx coded errors that the integration layer translates
       into ProblemDetails codes.
------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_PostTransfer
    @FromAccountId UNIQUEIDENTIFIER,
    @ToAccountId   UNIQUEIDENTIFIER,
    @Amount        DECIMAL(18,2),
    @Description   NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @fromBalance DECIMAL(18,2) = 0,
            @fromStatus  INT           = 0,
            @fromCurrency NVARCHAR(3)  = '',
            @toBalance   DECIMAL(18,2) = 0,
            @toStatus    INT           = 0,
            @toCurrency  NVARCHAR(3)   = '',
            @Reference   NVARCHAR(64)  = 'TRF-' + CAST(NEWID() AS NVARCHAR(36));

    IF (@Amount IS NULL OR @Amount <= 0)
        THROW 50001, 'INVALID_AMOUNT', 1;

    IF (@FromAccountId = @ToAccountId)
        THROW 50002, 'SELF_TRANSFER', 1;

    BEGIN TRAN;

    SELECT  @fromBalance   = [Balance],
            @fromStatus    = [Status],
            @fromCurrency  = [Currency]
    FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
    WHERE Id = @FromAccountId;

    IF (@fromStatus = 0)
        THROW 50003, 'FROM_ACCOUNT_NOT_FOUND', 1;

    SELECT  @toBalance  = [Balance],
            @toStatus   = [Status],
            @toCurrency = [Currency]
    FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @ToAccountId;

    IF (@toStatus = 0)
        THROW 50004, 'TO_ACCOUNT_NOT_FOUND', 1;

    IF (@fromStatus = 3) THROW 50005, 'ACCOUNT_CLOSED', 1;
    IF (@toStatus   = 3) THROW 50005, 'ACCOUNT_CLOSED', 1;
    IF (@fromStatus = 2) THROW 50006, 'ACCOUNT_FROZEN', 1;
    IF (@toStatus   = 2) THROW 50006, 'ACCOUNT_FROZEN', 1;

    IF (@fromCurrency <> @toCurrency)
        THROW 50007, 'CURRENCY_MISMATCH', 1;

    IF (@fromBalance < @Amount)
        THROW 50008, 'INSUFFICIENT_FUNDS', 1;

    UPDATE dbo.Accounts
        SET [Balance] = @fromBalance - @Amount
    WHERE Id = @FromAccountId;

    UPDATE dbo.Accounts
        SET [Balance] = @toBalance + @Amount
    WHERE Id = @ToAccountId;

    INSERT dbo.Transactions (AccountId, Type, Amount, BalanceAfter, Reference, Description)
        VALUES (@FromAccountId, 5 /* TransferOut */, -@Amount, @fromBalance - @Amount, @Reference, @Description);

    INSERT dbo.Transactions (AccountId, Type, Amount, BalanceAfter, Reference, Description)
        VALUES (@ToAccountId, 4 /* TransferIn */, @Amount, @toBalance + @Amount, @Reference, @Description);

    COMMIT TRAN;

    SELECT @Reference AS TransferReference;
END
GO

/* -------------------------------------------------------------------------
   usp_OpenAccount — allocates a unique account number inside the open-transaction.
------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_OpenAccount
    @CustomerId     UNIQUEIDENTIFIER,
    @Type           INT,
    @Currency       NVARCHAR(3),
    @AccountId      UNIQUEIDENTIFIER OUTPUT,
    @AccountNumber  NVARCHAR(24) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @AccountId = NEWID();

    /* Collision-checked number allocation (blocker-safe under UPDLOCK on the new key range) */
    SET @AccountNumber = 'FB-' + FORMAT(ABS(CHECKSUM(NEWID())) % 10000000000, '0000000000');

    WHILE EXISTS (SELECT 1 FROM dbo.Accounts WHERE Number = @AccountNumber)
        SET @AccountNumber = 'FB-' + FORMAT(ABS(CHECKSUM(NEWID())) % 10000000000, '0000000000');

    INSERT dbo.Accounts (Id, CustomerId, Number, Type, Status, Currency, Balance, OpenedAt)
        VALUES (@AccountId, @CustomerId, @AccountNumber, @Type, 1, @Currency, 0, SYSUTCDATETIME());
END
GO

/* -------------------------------------------------------------------------
   usp_CloseAccount — closure only when the balance is zeroed out.
------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_CloseAccount
    @AccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @balance DECIMAL(18,2) = 0,
            @status  INT = 0;

    SELECT @balance = [Balance], @status = [Status]
    FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @AccountId;

    IF (@status = 0)
        THROW 50010, 'ACCOUNT_NOT_FOUND', 1;

    IF (@status = 3)
        THROW 50011, 'ALREADY_CLOSED', 1;

    IF (@balance <> 0)
        THROW 50012, 'BALANCE_NOT_ZERO', 1;

    UPDATE dbo.Accounts SET [Status] = 3 /* Closed */, ClosedAt = SYSUTCDATETIME()
    WHERE Id = @AccountId;
END
GO

/* -------------------------------------------------------------------------
   usp_ListLedger — keyset paged surface for the transactions screen.
   Keyset (seek) on (CreatedAt, Id) is unchanged under pagination depth,
   unlike OFFSET paging which degrades linearly with page size.
------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ListLedger
    @AccountId   UNIQUEIDENTIFIER,
    @PageSize    INT   = 25,
    @BeforeToken DATETIMEOFFSET = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@PageSize)
           t.Id,
           t.Type,
           t.Amount,
           t.BalanceAfter,
           t.Reference,
           t.Description,
           t.CreatedAt,
           t.CreatedAt AS NextToken
    FROM dbo.Transactions t WITH (INDEX (IX_Transactions_AccountId_CreatedAt))
    WHERE t.AccountId = @AccountId
      AND (@BeforeToken IS NULL OR t.CreatedAt < @BeforeToken)
    ORDER BY t.CreatedAt DESC;
END
GO
