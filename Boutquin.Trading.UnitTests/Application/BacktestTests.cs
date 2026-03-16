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

/// <summary>
/// Tests for D9: Backtest.AnalyzePerformanceMetrics with empty equity curve.
/// </summary>
public sealed class BacktestTests
{
    /// <summary>
    /// D9: Calling AnalyzePerformanceMetrics before RunAsync (empty equity curve)
    /// should throw InvalidOperationException, not crash with divide-by-zero.
    /// </summary>
    [Fact]
    public void Backtest_AnalyzePerformanceMetrics_EmptyCurve_Throws()
    {
        // Arrange — create backtest with mocked dependencies (equity curve will be empty)
        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());

        var benchmarkPortfolio = new Mock<IPortfolio>();
        benchmarkPortfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal>());
        benchmarkPortfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy>());

        var fetcher = new Mock<IMarketDataFetcher>();

        var backtest = new BackTest(portfolio.Object, benchmarkPortfolio.Object, fetcher.Object, CurrencyCode.USD);

        // Act & Assert — should throw, not divide by zero
        var act = () => backtest.AnalyzePerformanceMetrics();
        act.Should().Throw<InvalidOperationException>();
    }
}
