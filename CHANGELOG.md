# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-30

First production release of the Boutquin.Trading quantitative trading framework for long-only ETF and equity backtesting.

### Core Engine
- Event-driven backtesting pipeline: MarketEvent → SignalEvent → OrderEvent → FillEvent
- Next-bar Open fills with quantity-limiting (no look-ahead bias)
- Multi-currency cash management and position tracking
- Burn-in period support for indicator warm-up
- Trading calendar integration with configurable composition modes
- Dividend reinvestment (DRIP) — optional whole-share reinvestment at Close price
- Expense ratio deduction — portfolio-level default + per-asset overrides in basis points

### Portfolio Construction (18 Models + 1 Decorator)
- EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity
- MaximumDiversification (Chopin & Briand 2008)
- HierarchicalRiskParity (Lopez de Prado 2016), HERC, ReturnTiltedHRP (Lohre et al. 2020)
- BlackLitterman, DynamicBlackLitterman
- MeanDownsideRisk with pluggable CVaR and DownsideDeviation measures
- RobustMeanVariance (minimax across covariance scenarios)
- TacticalOverlay, VolatilityTargeting, WeightConstrained, RegimeWeightConstrained
- TurnoverPenalized decorator with L1 penalty

### Covariance Estimation (4)
- Sample (N-1), EWMA, Ledoit-Wolf Shrinkage (with rho correction), Denoised (RMT)

### Downside Risk Measures (3)
- CVaR (Rockafellar-Uryasev 2000), DownsideDeviation, CDaR

### Analytics & Reporting
- Brinson-Fachler attribution, multi-factor OLS regression, correlation analysis
- Drawdown analysis, walk-forward optimization, Monte Carlo simulation
- Effective Number of Bets (Meucci 2009)
- HTML tearsheet with SVG charts, benchmark comparison report

### Risk Management
- Composite risk manager with MaxDrawdown, MaxPositionSize, MaxSectorExposure rules
- DrawdownCircuitBreaker for dynamic intervention

### Data Providers
- Tiingo, TwelveData (equities), Frankfurter (FX), FRED (economic), Fama-French (factors), CSV

### Infrastructure
- L1 memory cache + L2 CSV write-through (6 decorators)
- Full DI registration with explicit factory switches
- CancellationToken on all async APIs, structured logging
- 39 domain interfaces, 17 enums, tax engine extension points (6 interfaces, 9 domain records)
- 1,456 tests (1,452 unit + 4 architecture), cross-language verification against Python (81 golden vectors)
- .NET 10 / C# 14, TreatWarningsAsErrors, SourceLink, MinVer, Apache 2.0
