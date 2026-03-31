# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in Boutquin.Trading, please report it responsibly.

**Do not open a public issue for security vulnerabilities.**

Instead, please use [GitHub Security Advisories](https://github.com/boutquin/Boutquin.Trading/security/advisories/new) to report the vulnerability privately.

### What to include

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

### Response timeline

- **Acknowledgment** — within 48 hours
- **Initial assessment** — within 7 days
- **Fix or mitigation** — target within 30 days, depending on severity

### Scope

This policy covers the Boutquin.Trading NuGet packages and source code. Third-party dependencies (e.g., .NET runtime, NuGet packages) should be reported to their respective maintainers.

## Security Best Practices for Users

- **API keys** — Never hardcode API keys in source code. Use environment variables or secure configuration providers.
- **Data fetchers** — The data fetcher implementations make outbound HTTP calls. Ensure your network policies allow the required endpoints (Tiingo, Twelve Data, Frankfurter, FRED, Ken French Data Library).
- **EF Core** — If using the `DataAccess` project, follow standard database security practices (parameterized queries are enforced by EF Core).
