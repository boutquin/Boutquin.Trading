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

using Boutquin.Trading.Domain.Helpers;

namespace Boutquin.Trading.Tests.UnitTests.Domain;

public sealed class TearsheetTests
{
    [Fact]
    public void Tearsheet_ShouldStoreAllProperties()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2020, 1, 2)] = 100_000m,
            [new DateOnly(2020, 1, 3)] = 101_000m,
            [new DateOnly(2020, 1, 6)] = 99_500m,
        };

        var drawdowns = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2020, 1, 2)] = 0m,
            [new DateOnly(2020, 1, 3)] = 0m,
            [new DateOnly(2020, 1, 6)] = -0.0149m,
        };

        var ts = new Tearsheet(
            AnnualizedReturn: 0.12m,
            SharpeRatio: 1.5m,
            SortinoRatio: 2.0m,
            MaxDrawdown: -0.15m,
            CAGR: 0.11m,
            Volatility: 0.08m,
            Alpha: 0.03m,
            Beta: 0.95m,
            InformationRatio: 0.5m,
            EquityCurve: equityCurve,
            Drawdowns: drawdowns,
            MaxDrawdownDuration: 45,
            CalmarRatio: 0.73m,
            OmegaRatio: 1.2m,
            HistoricalVaR: -0.02m,
            ConditionalVaR: -0.03m,
            Skewness: -0.5m,
            Kurtosis: 3.2m,
            WinRate: 0.55m,
            ProfitFactor: 1.3m,
            RecoveryFactor: 2.1m);

        ts.AnnualizedReturn.Should().Be(0.12m);
        ts.SharpeRatio.Should().Be(1.5m);
        ts.SortinoRatio.Should().Be(2.0m);
        ts.MaxDrawdown.Should().Be(-0.15m);
        ts.CAGR.Should().Be(0.11m);
        ts.Volatility.Should().Be(0.08m);
        ts.Alpha.Should().Be(0.03m);
        ts.Beta.Should().Be(0.95m);
        ts.InformationRatio.Should().Be(0.5m);
        ts.EquityCurve.Should().HaveCount(3);
        ts.Drawdowns.Should().HaveCount(3);
        ts.MaxDrawdownDuration.Should().Be(45);
        ts.CalmarRatio.Should().Be(0.73m);
        ts.OmegaRatio.Should().Be(1.2m);
        ts.HistoricalVaR.Should().Be(-0.02m);
        ts.ConditionalVaR.Should().Be(-0.03m);
        ts.Skewness.Should().Be(-0.5m);
        ts.Kurtosis.Should().Be(3.2m);
        ts.WinRate.Should().Be(0.55m);
        ts.ProfitFactor.Should().Be(1.3m);
        ts.RecoveryFactor.Should().Be(2.1m);
    }

    [Fact]
    public void Tearsheet_ToString_ShouldContainAllMetricNames()
    {
        var ts = new Tearsheet(
            AnnualizedReturn: 0.12m,
            SharpeRatio: 1.5m,
            SortinoRatio: 2.0m,
            MaxDrawdown: -0.15m,
            CAGR: 0.11m,
            Volatility: 0.08m,
            Alpha: 0.03m,
            Beta: 0.95m,
            InformationRatio: 0.5m,
            EquityCurve: new SortedDictionary<DateOnly, decimal>(),
            Drawdowns: new SortedDictionary<DateOnly, decimal>(),
            MaxDrawdownDuration: 45,
            CalmarRatio: 0.73m,
            OmegaRatio: 1.2m,
            HistoricalVaR: -0.02m,
            ConditionalVaR: -0.03m,
            Skewness: -0.5m,
            Kurtosis: 3.2m,
            WinRate: 0.55m,
            ProfitFactor: 1.3m,
            RecoveryFactor: 2.1m);

        var str = ts.ToString();
        str.Should().NotBeNullOrWhiteSpace();
        str.Should().Contain("Annualized");
        str.Should().Contain("Sharpe");
    }

    [Fact]
    public void Tearsheet_RecordEquality_ShouldWork()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>();
        var drawdowns = new SortedDictionary<DateOnly, decimal>();

        var ts1 = new Tearsheet(0.12m, 1.5m, 2.0m, -0.15m, 0.11m, 0.08m, 0.03m, 0.95m, 0.5m,
            equityCurve, drawdowns, 45, 0.73m, 1.2m, -0.02m, -0.03m, -0.5m, 3.2m, 0.55m, 1.3m, 2.1m);
        var ts2 = new Tearsheet(0.12m, 1.5m, 2.0m, -0.15m, 0.11m, 0.08m, 0.03m, 0.95m, 0.5m,
            equityCurve, drawdowns, 45, 0.73m, 1.2m, -0.02m, -0.03m, -0.5m, 3.2m, 0.55m, 1.3m, 2.1m);

        ts1.Should().Be(ts2);
    }
}
