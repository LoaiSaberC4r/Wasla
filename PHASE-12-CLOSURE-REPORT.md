# Phase 12 Closure Report

Branch: `codex/phase-12-payments-refunds-revenue`

## File inventory

Added: `PHASE-12-API-CHANGES.md`, `PHASE-12-CLOSURE-REPORT.md`, `SOURCE-OF-TRUTH-PHASE-12-ADDENDUM.md`; `FinanceMutationsController.cs`, `FinanceReadController.cs`; `Features/Finance/FinancialMutations.cs`, `FinanceReadContracts.cs`, `Common/IFinancialNumberAllocator.cs`, `Common/FinancialWriteSupport.cs`; `Domain/Payments/FinancialRuntime.cs`, `Refund.cs`; `Persistence/Configurations/FinancialConfigurations.cs`, `FinanceReadService.cs`, `FinanceReadService.Revenue.cs`, `FinancialNumberAllocator.cs`; migration `20260930094425_Phase12PaymentsRefundsRevenue.cs` and its Designer; five `Phase12*Tests.cs` files.

Modified: `README.md`; `TicketsController.cs`; Ticket application Check-In, Force Check-In, Walk-In, Cancel, Details, RestoreNoShow, shared workflow and contracts; `Domain/Payments/Payment.cs`, `Domain/Tickets/Ticket.cs`, `Domain/Security/SystemSecurity.cs`, English and Arabic error resources; EF dependency injection, Reservation and Ticket configurations, model snapshot, Ticket queue reader, DbContext, and security seeder.

## Implemented scope

- Reception admission records one full Cash, Card, or Wallet Payment in EGP with the Ticket, including Check-In, Force Check-In, and Walk-In. Payment and Refund numbers use a persisted Practice/day/type counter.
- Explicit full Refund requires a cancelled, never-served Ticket. NoShow can be cancelled and refunded. Original Payment stays Paid; refunded NoShow cannot be restored.
- Payment and Refund metadata corrections require RowVersion, a reason, an actual change, and append-only old/new audit history. Financial writes use idempotency keys and resource locks. Database uniqueness protects one Refund per Payment.
- Practice financial lists, detail, and receipts are scoped to assigned Reception or owning Doctor. Patients can read their own public history and receipts. SuperAdmin receives grouped aggregates only.
- Doctor dashboard provides gross, refunds, net, counts, and chart-ready daily, Practice, method, Segment, and Visit Type datasets from financial transactions grouped in SQL.
- Added Phase 12 permission grants, English/Arabic errors, API documentation, and the source-of-truth addendum.

## Migration

`20260930094425_Phase12PaymentsRefundsRevenue` adds Refunds, correction histories, financial counters and idempotency records, Payment financial fields, indexes, checks, and restrictive foreign keys. Existing Payments receive EGP, their Ticket BusinessDate, deterministic PAY numbers, and the internal `LegacyUnspecified` method; counters continue after migrated rows. Generated SQL was reviewed for backfill and uniqueness and contains no table or column drop.

## Targeted verification

| Command | Result |
| --- | --- |
| `dotnet build Wasla.sln --no-restore -v minimal` | Passed; 0 warnings, 0 errors |
| `dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~Phase12" -v minimal` | 7 passed |
| `dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~Phase12" -v minimal` | 4 passed |
| `dotnet test tests/Wasla.Tests.Unit/Wasla.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~Phase11TicketDomainTests" -v minimal` | 10 passed |
| `dotnet test tests/Wasla.Tests.Integration/Wasla.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~Phase11TicketPersistenceTests" -v minimal` | 5 passed |
| `dotnet ef migrations has-pending-model-changes --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --no-build` | No pending model changes |
| `dotnet ef migrations script 20260917125705_Phase11TicketQueueRuntime 20260930094425_Phase12PaymentsRefundsRevenue --project src/Wasla/Wasla.Infrastructure.EntityFrameworkCore.SqlServer --startup-project src/Wasla/Wasla.Api --context WaslaDbContext --no-build --output "$env:TEMP\WaslaPhase12Migration.sql"` | Generated successfully; reviewed |

Focused integration coverage includes paid admission and replay, Cancel eligibility, full Refund and correction, persisted InProgress rejection, stale-writer Refund uniqueness, refunded NoShow restore rejection, revenue/date grouping, patient receipt privacy, and Reception Practice denial.

## Remaining verification

The migration has not been applied to a live SQL Server database in this workspace. The SQL Server application locks and number allocator were reviewed, while the stale-writer uniqueness test ran on SQLite; a live multi-connection SQL Server contention check remains for deployment validation.
