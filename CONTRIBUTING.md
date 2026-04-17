# Contributing to Boutquin.Trading

Thank you for considering contributing to Boutquin.Trading! Whether it's reporting a bug, proposing a feature, or submitting a pull request, your input is welcome.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How to Contribute](#how-to-contribute)
  - [Reporting Bugs](#reporting-bugs)
  - [Suggesting Enhancements](#suggesting-enhancements)
  - [Contributing Code](#contributing-code)
- [Style Guides](#style-guides)
  - [Git Commit Messages](#git-commit-messages)
  - [C# Style Guide](#c-style-guide)
  - [Documentation Style Guide](#documentation-style-guide)
  - [Financial Calculation Conventions](#financial-calculation-conventions)
- [Pull Request Process](#pull-request-process)
- [License](#license)
- [Community](#community)

## Code of Conduct

This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Report unacceptable behavior through [GitHub Issues](https://github.com/boutquin/Boutquin.Trading/issues).

## How to Contribute

### Reporting Bugs

Open an issue on the [Issues](https://github.com/boutquin/Boutquin.Trading/issues) page with:

- A clear and descriptive title.
- Steps to reproduce the issue (ideally a minimal failing code snippet or backtest configuration).
- Expected and actual output, including numeric values and tolerances where relevant.
- Reference values (from a Python reference implementation, academic paper, or established library) when asserting calculation correctness.
- Environment: OS, .NET runtime version, package version.

### Suggesting Enhancements

Open an issue describing:

- The trading algorithm, construction model, estimator, or analytics method you would like added.
- A primary reference (paper, textbook, or industry standard) with the algorithm's published form.
- The intended consumer (portfolio construction, risk management, analytics, etc.).
- Any trade-offs: precision, memory profile, computational cost, data requirements.

### Contributing Code

1. **Fork the repository** and clone your fork locally.
   ```bash
   git clone https://github.com/your-username/Boutquin.Trading.git
   cd Boutquin.Trading
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature-or-bugfix-name
   ```

3. **Implement the change** following the style guides below.

4. **Add tests** covering the new behavior. For algorithmic code, prefer:
   - xUnit + FluentAssertions unit tests under `tests/UnitTests/` with explicit tolerances.
   - Cross-language reference vectors under `tests/Verification/` when a Python reference (numpy / scipy / pypfopt) can establish ground truth.
   - Architecture assertions under `tests/ArchitectureTests/` when the change introduces new public types or cross-project dependencies.

5. **Record the public API surface**. If your change adds or removes public types or members in `src/Domain/` or `src/Application/`, update `PublicAPI.Unshipped.txt` in the relevant project directory. The `PublicAPI` analyzer enforces this at build time — an unrecorded symbol is a build error (RS0016).

6. **Update `CHANGELOG.md`** under the `[Unreleased]` section using the appropriate heading (`### Added`, `### Changed`, `### Fixed`, etc.).

7. **Run the full gate** before opening a PR:
   ```bash
   dotnet build Boutquin.Trading.slnx --configuration Release
   dotnet test Boutquin.Trading.slnx --configuration Release
   dotnet format Boutquin.Trading.slnx --verify-no-changes
   ```

8. **Push and open a pull request**.

## Style Guides

### Git Commit Messages

- Use the present tense ("Add MeanCVaR construction model" not "Added MeanCVaR construction model").
- Use the imperative mood ("Wire risk manager to DI" not "Wires risk manager to DI").
- Limit the first line to 72 characters.
- Reference issues and pull requests where applicable.

### C# Style Guide

- Follow the conventions documented in `CLAUDE.md` and `.editorconfig` at the repository root.
- Public types are `sealed` unless they are interfaces or abstract base classes (enforced by architecture tests).
- Architecture constraint: `Domain` must not reference `Application` or any `Data.*` project. `Application` may reference `Domain` and `Data.CSV` only. Data provider projects (`Data.Tiingo`, `Data.Fred`, etc.) may reference `Domain` only. Violations are caught by architecture tests.
- No EF Core or infrastructure packages in `src/Domain/` — domain types must remain portable.
- The project uses `<Nullable>enable</Nullable>` globally. Do not use `#nullable disable` except in auto-generated EF migration files. Suppress CS8625 null-literal tests with `null!` rather than disabling NRT analysis.

### Documentation Style Guide

All public API additions must satisfy the in-code documentation bar:

- `<summary>` on every public type, constructor, method, property, and enum member.
- `<param>`, `<returns>`, `<exception>`, and `<remarks>` per the required-elements checklist.
- No banned boilerplate phrases ("Provides the ... functionality", "Executes the ... operation", "Gets or sets the ... for this instance", "Input value for ...", "Operation result.", "Gets the ...").
- Algorithmic references: name the paper, author, year, and (where applicable) arXiv number or DOI.
- Construction models and covariance estimators must document: the objective function, the optimization or shrinkage approach, the convergence contract, and any PSD guarantees (or lack thereof).

Validation commands:

```bash
# Enforce banned-phrase policy (must return zero matches).
rg -n "Provides the .* functionality|Executes the .* operation|Gets or sets the .* for this instance" src --glob '*.cs'

# Enforce low-signal phrase policy.
rg -n "Input value for <paramref name=|/// Executes |Operation result\." src --glob '*.cs'

# Enforce accessor-verb property doc policy.
rg -n "/// Gets the " src --glob '*.cs'
```

### Financial Calculation Conventions

These conventions apply to all financial metric and algorithm implementations:

- **Sample divisor (N-1)** — All deviation, variance, and covariance calculations use the sample divisor (`Length - 1`), not the population divisor (`Length`). This is standard for financial time series where values are samples from an unknown distribution.
- **`CalculationException` for degenerate inputs** — When a computation would produce `NaN`, `Infinity`, or a mathematically undefined result (zero denominator in a ratio, non-positive base in `Math.Pow`, etc.), throw `Boutquin.Trading.Domain.Exceptions.CalculationException`. Do not silently return zero or propagate `NaN`.
- **Raw decimal ratios, not percentages** — Return values are raw decimal ratios (e.g., `0.125` for 12.5%). Never multiply by 100 inside a computation method. Derived metrics that compose multiple ratios (e.g., Calmar = CAGR / |MaxDrawdown|) depend on consistent units.
- **`decimal` for financial quantities** — Portfolio weights, returns, prices, and risk metrics use `decimal`. `double` is acceptable only for intermediate calculations in matrix operations or transcendental functions where precision loss is documented and bounded.
- **Array length validation on paired inputs** — Methods accepting two correlated arrays (e.g., portfolio returns and benchmark returns) must guard against mismatched lengths with `ArgumentException` before any computation.
- **Next-bar Open fills** — Signals generated on bar T queue pending orders that fill at bar T+1's Open price. This eliminates look-ahead bias and matches the zipline / QuantConnect / backtrader convention.
- **Trading-day durations** — Drawdown duration and similar time-based metrics count equity curve entries (each representing one trading day), not calendar days.

## Pull Request Process

1. **Ensure the full gate passes**: build (warnings-as-errors), unit tests, architecture tests, and `dotnet format --verify-no-changes`.
   ```bash
   dotnet build Boutquin.Trading.slnx --configuration Release
   dotnet test Boutquin.Trading.slnx --configuration Release
   dotnet format Boutquin.Trading.slnx --verify-no-changes
   ```
2. **Describe your changes** in the PR body: reference the issue, summarize the algorithm or fix, link the primary reference, and note any `PublicAPI.Unshipped.txt` entries added.
3. **Review process**: maintainers will review for correctness, financial convention adherence, and architectural fit. You may be asked to tighten tolerances, add cross-language reference vectors, or adjust public API surface.
4. **Merge**: once approved, a maintainer merges the PR. Releases are cut separately via the dual-repo squash workflow on the public repository.

## License

By contributing to Boutquin.Trading, you agree that your contributions are licensed under the Apache 2.0 License.

## Community

Join the [GitHub Discussions](https://github.com/boutquin/Boutquin.Trading/discussions) to ask questions, propose algorithms, and share usage patterns.

---

Thank you for contributing!
