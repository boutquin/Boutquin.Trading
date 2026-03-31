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

using Boutquin.Trading.Application.Reporting;
using Boutquin.Trading.Domain.Helpers;
using FluentAssertions;

/// <summary>
/// Tests for the HTML report generator.
/// </summary>
public sealed class HtmlReportGeneratorTests
{
    private static Tearsheet CreateSampleTearsheet()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2020, 1, 2), 10000m },
            { new DateOnly(2020, 1, 3), 10100m },
            { new DateOnly(2020, 1, 6), 10050m },
            { new DateOnly(2020, 1, 7), 10200m },
            { new DateOnly(2020, 1, 8), 10150m }
        };

        var drawdowns = new SortedDictionary<DateOnly, decimal>
        {
            { new DateOnly(2020, 1, 2), 0m },
            { new DateOnly(2020, 1, 3), 0m },
            { new DateOnly(2020, 1, 6), -0.00495m },
            { new DateOnly(2020, 1, 7), 0m },
            { new DateOnly(2020, 1, 8), -0.00490m }
        };

        return new Tearsheet(
            AnnualizedReturn: 0.12m,
            SharpeRatio: 1.5m,
            SortinoRatio: 2.0m,
            MaxDrawdown: -0.05m,
            CAGR: 0.11m,
            Volatility: 0.08m,
            Alpha: 0.02m,
            Beta: 0.9m,
            InformationRatio: 0.5m,
            EquityCurve: equityCurve,
            Drawdowns: drawdowns,
            MaxDrawdownDuration: 3,
            CalmarRatio: 2.2m,
            OmegaRatio: 1.8m,
            HistoricalVaR: -0.02m,
            ConditionalVaR: -0.03m,
            Skewness: -0.1m,
            Kurtosis: 0.5m,
            WinRate: 0.55m,
            ProfitFactor: 1.3m,
            RecoveryFactor: 2.4m);
    }

    // --- RP3-05 Test: Generated HTML is valid (contains expected structure) ---

    [Fact]
    public void Generate_ShouldProduceValidHtml()
    {
        var tearsheet = CreateSampleTearsheet();

        var html = HtmlReportGenerator.GenerateTearsheetReport(tearsheet, "Test Strategy");

        html.Should().Contain("<!DOCTYPE html", "Should start with DOCTYPE");
        html.Should().Contain("<html", "Should contain html tag");
        html.Should().Contain("</html>", "Should have closing html tag");
        html.Should().Contain("Test Strategy", "Should contain strategy name");
    }

    // --- RP3-05 Test: Contains all expected chart sections ---

    [Fact]
    public void Generate_ShouldContainAllChartSections()
    {
        var tearsheet = CreateSampleTearsheet();

        var html = HtmlReportGenerator.GenerateTearsheetReport(tearsheet, "Test Strategy");

        html.Should().Contain("Equity Curve", "Should contain equity curve section");
        html.Should().Contain("Drawdown", "Should contain drawdown section");
        html.Should().Contain("Monthly Returns", "Should contain monthly returns section");
    }

    // --- RP3-05 Test: Contains key metrics ---

    [Fact]
    public void Generate_ShouldContainKeyMetrics()
    {
        var tearsheet = CreateSampleTearsheet();

        var html = HtmlReportGenerator.GenerateTearsheetReport(tearsheet, "Test Strategy");

        html.Should().Contain("Sharpe Ratio", "Should contain Sharpe Ratio");
        html.Should().Contain("Sortino Ratio", "Should contain Sortino Ratio");
        html.Should().Contain("Max Drawdown", "Should contain Max Drawdown");
        html.Should().Contain("CAGR", "Should contain CAGR");
        html.Should().Contain("Volatility", "Should contain Volatility");
        html.Should().Contain("Calmar Ratio", "Should contain Calmar Ratio");
        html.Should().Contain("VaR", "Should contain VaR");
    }

    // --- RP3-05 Test: File size < 2MB for small dataset ---

    [Fact]
    public void Generate_ShouldBeSmallerThan2MB()
    {
        var tearsheet = CreateSampleTearsheet();

        var html = HtmlReportGenerator.GenerateTearsheetReport(tearsheet, "Test Strategy");

        var sizeInBytes = System.Text.Encoding.UTF8.GetByteCount(html);
        sizeInBytes.Should().BeLessThan(2 * 1024 * 1024, "Report should be smaller than 2MB");
    }
}
