// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Domain.Extensions;
using Boutquin.Trading.Application.Reporting;
using Boutquin.Trading.Domain.Helpers;
using FluentAssertions;

/// <summary>
/// Tests for the benchmark comparison report.
/// </summary>
public sealed class BenchmarkComparisonReportTests
{
    private const decimal Precision = 1e-10m;

    private static Tearsheet CreateTearsheet(
        decimal annReturn, decimal sharpe, decimal sortino, decimal maxDd,
        decimal cagr, decimal vol, decimal alpha, decimal beta, decimal ir)
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2020, 1, 2), 10000m },
            { new DateOnly(2020, 1, 3), 10100m },
            { new DateOnly(2020, 1, 6), 10200m }
        };

        var drawdowns = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2020, 1, 2), 0m },
            { new DateOnly(2020, 1, 3), 0m },
            { new DateOnly(2020, 1, 6), 0m }
        };

        return new Tearsheet(
            annReturn, sharpe, sortino, maxDd, cagr, vol, alpha, beta, ir,
            equityCurve, drawdowns, 0,
            CalmarRatio: 2.0m, OmegaRatio: 1.5m,
            HistoricalVaR: -0.02m, ConditionalVaR: -0.03m,
            Skewness: 0m, Kurtosis: 0m,
            WinRate: 0.55m, ProfitFactor: 1.2m, RecoveryFactor: 2.0m);
    }

    // --- RP3-06 Test: Benchmark line appears on equity chart ---

    [Fact]
    public void Generate_ShouldContainBenchmarkLine()
    {
        var portfolio = CreateTearsheet(0.12m, 1.5m, 2.0m, -0.05m, 0.11m, 0.08m, 0.02m, 0.9m, 0.5m);
        var benchmark = CreateTearsheet(0.10m, 1.2m, 1.5m, -0.03m, 0.09m, 0.07m, 0m, 1.0m, 0m);

        var html = BenchmarkComparisonReport.Generate(
            portfolio, benchmark, "My Strategy", "S&P 500");

        html.Should().Contain("My Strategy", "Should contain portfolio name");
        // HTML-encoded ampersand
        html.Should().Contain("S&amp;P 500", "Should contain HTML-encoded benchmark name");
    }

    // --- RP3-06 Test: Tracking error calculated correctly ---

    [Fact]
    public void CalculateTrackingError_ShouldReturnCorrectValue()
    {
        // Active returns = portfolio - benchmark
        var portfolioReturns = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m };
        var benchmarkReturns = new[] { 0.005m, -0.015m, 0.025m, -0.005m, 0.015m };

        var trackingError = BenchmarkComparisonReport.CalculateTrackingError(portfolioReturns, benchmarkReturns);

        // Active returns: 0.005, -0.005, 0.005, -0.005, 0.005
        // StdDev of active returns (sample) = sqrt(variance)
        // Mean = 0, Var = 5*0.000025/4 = 0.00003125, StdDev = 0.00559016...
        trackingError.Should().BeGreaterThan(0m, "Tracking error should be positive");

        var activeReturns = portfolioReturns.Zip(benchmarkReturns, (p, b) => p - b).ToArray();
        var expectedTrackingError = activeReturns.StandardDeviation() * (decimal)Math.Sqrt(252);
        trackingError.Should().BeApproximately(expectedTrackingError, Precision);
    }

    // --- RP3-06 Test: Metrics table contains both portfolio and benchmark columns ---

    [Fact]
    public void Generate_ShouldContainBothColumns()
    {
        var portfolio = CreateTearsheet(0.12m, 1.5m, 2.0m, -0.05m, 0.11m, 0.08m, 0.02m, 0.9m, 0.5m);
        var benchmark = CreateTearsheet(0.10m, 1.2m, 1.5m, -0.03m, 0.09m, 0.07m, 0m, 1.0m, 0m);

        var html = BenchmarkComparisonReport.Generate(
            portfolio, benchmark, "My Strategy", "Benchmark");

        html.Should().Contain("Portfolio", "Should contain portfolio column");
        html.Should().Contain("Benchmark", "Should contain benchmark column");
        html.Should().Contain("Tracking Error", "Should contain tracking error");
    }

    // --- RP3-06 Test: Mismatched array lengths throw ---

    [Fact]
    public void CalculateTrackingError_MismatchedLengths_ShouldThrow()
    {
        var portfolioReturns = new[] { 0.01m, 0.02m, 0.03m };
        var benchmarkReturns = new[] { 0.01m, 0.02m };

        var act = () => BenchmarkComparisonReport.CalculateTrackingError(portfolioReturns, benchmarkReturns);

        act.Should().Throw<ArgumentException>();
    }
}
