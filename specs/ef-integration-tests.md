# Spec: ef-integration-tests

**Project:** Boutquin.Trading
**Status:** DRAFT
**Created:** 2026-03-16
**Version:** 1.0.0
**Parent:** fix-low-severity (finding #16, ExchangeHoliday index)
**Frozen-at:** none

**Paths:**
- Queue: none (review-driven)
- Snapshot: none
- Report: none

---

## Summary

Add EF Core integration tests using the in-memory database provider to verify entity configurations, seed data, constraints, and index behavior. The immediate trigger is the removal of the unique index on `ExchangeHoliday.Description` (fix-low-severity #16), which cannot be verified by a unit test alone. Beyond that, 14 entity configurations and their seed data have zero test coverage today.

**Why in-memory and not SQLite?** The project already references `Microsoft.EntityFrameworkCore.InMemory` in the DataAccess `.csproj`. In-memory is sufficient for verifying entity shapes, seed data, navigation properties, and basic constraints. Index uniqueness enforcement requires SQLite (in-memory provider ignores indexes), so index-specific tests will use the SQLite provider.

---

## Acceptance Criteria

- [ ] AC-1: New integration test project or test class exercises `SecurityMasterContext` with in-memory + SQLite providers
- [ ] AC-2: Each of the 14 entity configurations has at least one test verifying: entity can be saved/loaded, required properties are enforced, seed data is present
- [ ] AC-3: ExchangeHoliday test verifies two holidays with the same description on different exchanges can coexist (SQLite — validates index removal from #16)
- [ ] AC-4: ExchangeHoliday test verifies two holidays with the same exchange+date are rejected (SQLite — validates remaining composite unique index)
- [ ] AC-5: `dotnet build` succeeds with 0 warnings, 0 errors
- [ ] AC-6: `dotnet test` passes 100%

---

## Deliverables

### Phase 1 — Test Infrastructure

| # | Action | Detail |
|---|--------|--------|
| 1 | Add SQLite EF provider | Add `Microsoft.EntityFrameworkCore.Sqlite` package reference to UnitTests `.csproj` |
| 2 | Create test fixture | `DataAccess/SecurityMasterContextFixture.cs` — factory methods for in-memory and SQLite `SecurityMasterContext` instances |
| 3 | Create test class | `DataAccess/SecurityMasterContextTests.cs` |

**Fixture pattern:**
```csharp
public static class SecurityMasterContextFixture
{
    public static SecurityMasterContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<SecurityMasterContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new SecurityMasterContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static SecurityMasterContext CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SecurityMasterContext>()
            .UseSqlite(connection)
            .Options;
        var context = new SecurityMasterContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

### Phase 2 — Seed Data Verification Tests (In-Memory)

Verify each seeded entity table has the expected row count after `EnsureCreated()`.

| # | Entity | Expected Seed Count | Validates |
|---|--------|-------------------|-----------|
| 1 | `AssetClass` | 8 | CashAndCashEquivalents through Other |
| 2 | `Continent` | 7 | AF, AN, AS, EU, NA, OC, SA |
| 3 | `Currency` | All defined CurrencyCode values | Enum-based seed |
| 4 | `Country` | Seeded countries | Country-continent relationships |
| 5 | `City` | 9 | New York through Toronto |
| 6 | `Exchange` | Seeded exchanges | Exchange-city relationships |
| 7 | `SymbolStandard` | 5 | Cusip, Isin, Sedol, Ric, Bloomberg |
| 8 | `TimeZone` | Seeded time zones | Time zone entries |

### Phase 3 — Entity CRUD Tests (In-Memory)

| # | Test | Validates |
|---|------|-----------|
| 1 | `Security_CanSaveAndLoad` | Security entity round-trips through context |
| 2 | `SecuritySymbol_CanSaveWithValidSecurity` | FK relationship Security → SecuritySymbol |
| 3 | `SecurityPrice_CanSaveWithValidSecurity` | FK relationship Security → SecurityPrice |
| 4 | `FxRate_CanSaveAndLoad` | FxRate entity with DateOnly conversion |
| 5 | `ExchangeHoliday_CanSaveAndLoad` | ExchangeHoliday entity with DateOnly conversion |
| 6 | `ExchangeSchedule_CanSaveAndLoad` | ExchangeSchedule entity |
| 7 | `Position_IsNotAnEfEntity` | Verify Position is not tracked by context (it's an in-memory domain object) |

### Phase 4 — Index & Constraint Tests (SQLite)

SQLite enforces unique indexes; in-memory does not.

| # | Test | Validates |
|---|------|-----------|
| 1 | `ExchangeHoliday_SameDescriptionDifferentExchanges_Allowed` | Two holidays named "New Year" on NYSE and LSE can coexist (validates #16 fix) |
| 2 | `ExchangeHoliday_SameExchangeAndDate_Rejected` | Duplicate (ExchangeCode, HolidayDate) throws `DbUpdateException` |
| 3 | `FxRate_DuplicateRateDateBaseCurrencyQuoteCurrency_Rejected` | Composite unique index enforced |
| 4 | `AssetClass_DuplicateDescription_Rejected` | Unique index on Description enforced |
| 5 | `Continent_DuplicateName_Rejected` | Unique index on Name enforced |
| 6 | `Currency_DuplicateCode_Rejected` | PK enforced |

---

## Decision Rules

| When | Do |
|------|-----|
| In-memory provider doesn't enforce constraint | Use SQLite for that specific test |
| Entity requires seeded foreign key data | Use `EnsureCreated()` which applies seed data |
| Test needs clean database state | Create new in-memory DB with unique name per test |
| SecurityMasterContext constructor is `internal` | Add `InternalsVisibleTo` for the test project |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| SQLite type mapping differs from SQL Server (e.g., Date columns) | Use `HasColumnType("Date")` which SQLite maps to TEXT — acceptable for constraint tests |
| In-memory provider doesn't enforce FK constraints | Document which tests need SQLite; use SQLite for constraint/index tests |
| SecurityMasterContext may have constructor visibility issues | Check access modifier; add `InternalsVisibleTo` if needed |
| Large number of seeded entities makes tests brittle to count changes | Test `>= expected` rather than `== expected` for seed counts, or test known specific entries |

---

## Completion Standard

- [ ] All 14 configurations have at least one test
- [ ] ExchangeHoliday index behavior fully tested (both allowed and rejected cases)
- [ ] No test depends on test execution order
- [ ] Each test creates its own database instance (isolation)

---

## Close Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | All acceptance criteria marked `[x]` | |
| 2 | Build + tests clean | |
| 3 | No High/Medium tier regressions | |

---

## Changelog

| Date | Version | Change |
|------|---------|--------|
| 2026-03-16 | 1.0.0 | Initial spec — EF integration tests for all 14 entity configurations + index verification |
