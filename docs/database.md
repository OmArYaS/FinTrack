# FinTrack — Database design notes

## Schema surface

| Object | Kind | Purpose |
| --- | --- | --- |
| Customers, Accounts, Transactions | tables | EF Core 8 code-first baseline model |
| `UX_Transactions_Reference` | unique index | transfer pairing + idempotency backstop |
| `IX_Transactions_AccountId_CreatedAt` | index | ledger paging (descending seek) |
| `IX_Accounts_CustomerId` | FK index | per-customer account lists |
| `ux_/IX_Accounts_Number` | unique index | human-readable account numbers |
| `CK_Accounts_Balance` | check | invariant: never negative |
| `vw_AccountDailyTotals` | indexed view (SCHEMABINDING) | daily roll-ups for dashboards |
| `usp_PostTransfer` | stored procedure | atomic money movement with UPDLOCK |
| `usp_OpenAccount` / `usp_CloseAccount` | stored procedures | lifecycle invariants server-side |
| `usp_ListLedger` | stored procedure | keyset pagination for the ledger screen |

## Why both EF Core migrations and scripts?

- Migrations are the source of truth for environments the team ships through (App Service slots run
  them on startup via `DatabaseInitializer`).
- The DBA-reviewable scripts (`database/*.sql`) exist for locked-down environments where DDL changes
  go through change-approval, a common pattern in banking. Both are kept consistent with the model;
  the `Migration` class is generated from the same `FinTrackEntityConfigurations`.

## Indexing decisions

- Ledger is append-heavy with newest-first reads → `(AccountId, CreatedAt)` composite.
- Transfers always look accounts up by `Id` → free via clustered PK.
- Only `Number` needs an index for teller search → partial? Full unique index is cheap at this size
  and also enforces uniqueness in SQLite demo mode.
- Daily roll-ups pivot on `(AccountId, TxnDate)` → materialized in the indexed view.

## Consistency model

- Money movements run inside `READ COMMITTED` snapshot-friendly transactions (App-level).
- `usp_PostTransfer` takes `UPDLOCK` + `HOLDLOCK` for serialized read-modify-write.
- The `Accounts.RowVersion` rowversion token + EF Core optimistic concurrency covers the API path;
  `DbUpdateConcurrencyException` → HTTP 409 with `CONCURRENT_UPDATE`.

## Operational notes (MS SQL Server)

- Business Critical tier for ledger tables if you need read-replica isolation for reports.
- Short log backups: the ledger is append-only; PITR retained per compliance (demo repo default).
- Suggested per-tenant partitioning story lives in the security doc.
