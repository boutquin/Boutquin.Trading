## Summary

Brief description of what this PR does.

## Changes

- ...

## Related Issues

Closes #

## Checklist

- [ ] Code compiles with zero warnings (`TreatWarningsAsErrors` enabled)
- [ ] All existing unit, verification, and architecture tests pass
- [ ] New tests added for new functionality (unit + reference vectors where applicable)
- [ ] `PublicAPI.Unshipped.txt` updated for any new or changed public API
- [ ] `dotnet format --verify-no-changes` produces no changes
- [ ] XML doc comments are complete (no banned phrases; algorithmic references by author/year; construction models and estimators document objective, approach, convergence, and PSD guarantees — see [CONTRIBUTING.md](../CONTRIBUTING.md#documentation-style-guide))
- [ ] CHANGELOG.md updated under `[Unreleased]` (if user-facing change)
- [ ] No new `Boutquin.*` dependencies introduced that violate the architecture constraint (Domain ← Application ← Data.*; architecture tests enforce this)
