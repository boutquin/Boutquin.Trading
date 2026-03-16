# Spec: ca2007-configureawait

**Project:** Boutquin.Trading
**Status:** DRAFT
**Created:** 2026-03-16
**Version:** 1.0.0
**Parent:** fix-low-severity (finding #22, PROJ-I02)
**Frozen-at:** none

**Paths:**
- Queue: none (review-driven)
- Snapshot: none
- Report: none

---

## Summary

Remove the global CA2007 suppression from `Directory.Build.props` and add `ConfigureAwait(false)` to all `await` expressions in library code. Test projects get CA2007 added to their per-project `<NoWarn>` since test code runs on a synchronization context where `ConfigureAwait(false)` is unnecessary and `ConfigureAwait(true)` is idiomatic.

**Origin:** Low-severity finding #22 (PROJ-I02) from the deep code review, deferred from `fix-low-severity` due to blast radius. CA2007 is currently suppressed globally, masking missing `ConfigureAwait(false)` calls in library code. Library code (Domain, DataAccess, Application) must use `ConfigureAwait(false)` on all awaits to avoid deadlocks when consumed by UI or ASP.NET callers with a `SynchronizationContext`.

---

## Acceptance Criteria

- [ ] AC-1: CA2007 removed from global `<NoWarn>` in `Directory.Build.props`
- [ ] AC-2: CA2007 added to `<NoWarn>` in each test project `.csproj` (UnitTests, ArchitectureTests, BackTest)
- [ ] AC-3: All `await` expressions in library projects have `.ConfigureAwait(false)`
- [ ] AC-4: All `await using` statements in library projects have `.ConfigureAwait(false)`
- [ ] AC-5: `dotnet build` succeeds with 0 warnings, 0 errors
- [ ] AC-6: `dotnet test` passes 100%

---

## Deliverables

### Phase 1 — Build Config (no code changes)

| # | File | Action | Change |
|---|------|--------|--------|
| 1 | `Directory.Build.props` | Update | Remove `CA2007` from `<NoWarn>` |
| 2 | `Boutquin.Trading.UnitTests/Boutquin.Trading.UnitTests.csproj` | Update | Add `<NoWarn>$(NoWarn);CA2007</NoWarn>` |
| 3 | `Tests/Boutquin.Trading.Tests.ArchitectureTests/Boutquin.Trading.Tests.ArchitectureTests.csproj` | Update | Add `<NoWarn>$(NoWarn);CA2007</NoWarn>` |
| 4 | `Boutquin.Trading.BackTest/Boutquin.Trading.BackTest.csproj` | Update | Add `<NoWarn>$(NoWarn);CA2007</NoWarn>` |

After Phase 1, `dotnet build` will produce CA2007 errors in library code — these are the locations that need fixing.

### Phase 2 — Add ConfigureAwait(false) to library code

**Known locations (10 actual `await` statements, not counting comments):**

| # | File | Line(s) | Statement Type |
|---|------|---------|---------------|
| 1 | `Application/Brokers/SimulatedBrokerage.cs` | 76 | `await ... .FirstOrDefaultAsync()` |
| 2-7 | `Domain/Data/CsvMarketDataStorage.cs` | 93, 96, 100, 144, 147, 151 | `await using var ...` |
| 8-9 | `Domain/Data/CsvMarketDataFetcher.cs` | 87, 174 | `await using var ...` |
| 10 | `Domain/Helpers/CsvSymbolReader.cs` | 63 | `await using var ...` |

**Pattern for `await` expressions:**
```csharp
// Before:
var result = await SomeMethodAsync();
// After:
var result = await SomeMethodAsync().ConfigureAwait(false);
```

**Pattern for `await using` statements:**
```csharp
// Before:
await using var stream = new FileStream(...);
// After:
await using var stream = new FileStream(...).ConfigureAwait(false);
// Note: .ConfigureAwait(false) on IAsyncDisposable returns ConfiguredAsyncDisposable
```

### Phase 3 — Verify

Build with 0 warnings/errors. Run full test suite. Count of `await` without `ConfigureAwait` in library code should be 0.

---

## Decision Rules

| When | Do |
|------|-----|
| `await using` on a type that doesn't implement `IAsyncDisposable` | Use synchronous `using` instead; `ConfigureAwait` requires `IAsyncDisposable` |
| New `await` expressions added in future PRs | CI will catch them as CA2007 errors (TreatWarningsAsErrors is on) |
| `await foreach` expressions found | Use `.ConfigureAwait(false)` — same pattern as `await using` |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| `ConfigureAwait(false)` on `await using` requires `IAsyncDisposable` | `FileStream` and `StreamWriter` implement it; verify each case |
| Behavioral change in SimulatedBrokerage event handlers | Existing tests cover all order types; run full suite after change |
| Future PRs forget ConfigureAwait | CA2007 is now enforced as an error — CI catches it automatically |

---

## Close Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | All acceptance criteria marked `[x]` | |
| 2 | `grep -rn "await " --include="*.cs" | grep -v ConfigureAwait` returns 0 hits in library code | |
| 3 | Build + tests clean | |

---

## Changelog

| Date | Version | Change |
|------|---------|--------|
| 2026-03-16 | 1.0.0 | Initial spec — extracted from fix-low-severity finding #22 |
