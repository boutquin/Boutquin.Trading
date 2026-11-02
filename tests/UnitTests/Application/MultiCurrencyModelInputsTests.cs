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

using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.RiskManagement;
using Boutquin.Trading.Recipes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// The inputs a multi-currency portfolio hands its models and rules must be in base currency:
/// construction-model return windows, risk-rule position values, and the FX history a bounded run
/// can carry forward from.
/// </summary>
public sealed class MultiCurrencyModelInputsTests
{
    private static readonly Symbol s_vgsh = new("VGSH");
    private static readonly Symbol s_xbb = new("XBB");
    private static readonly DateOnly s_start = new(2024, 6, 3);

    // ─── Construction-model returns are unhedged base-currency returns ───

    [Fact]
    public void GenerateSignals_ForeignAssetWithFlatNativePrice_SeesFxReturnsInBase()
    {
        // VGSH is flat at 100 USD while USD/CAD moves ~1.5%/day; XBB drifts 0.02%/day in CAD (a flat
        // series would trip the model's zero-volatility guard). A CAD investor's VGSH return IS the
        // FX move, so the return-based model must see VGSH as the volatile asset.
        decimal[] usdCad = [1.30m, 1.32m, 1.31m, 1.34m, 1.33m, 1.36m, 1.35m, 1.37m, 1.36m, 1.38m, 1.40m];
        var marketData = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        var fx = new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>();
        for (var i = 0; i < usdCad.Length; i++)
        {
            var date = s_start.AddDays(i);
            marketData[date] = new SortedDictionary<Symbol, Bar>
            {
                [s_vgsh] = new Bar(date, 100m, 100m, 100m, 100m, 100m, 1_000),
                [s_xbb] = new Bar(date, 50m + 0.01m * (i % 2), 50m, 50m, 50m, 50m + 0.01m * (i % 2), 1_000),
            };
            fx[date] = new SortedDictionary<Currency, decimal> { [Currency.USD] = usdCad[i] };
        }

        var strategy = new ConstructionModelStrategy(
            "Test",
            new Dictionary<Symbol, Currency> { [s_vgsh] = Currency.USD, [s_xbb] = Currency.CAD },
            new SortedDictionary<Currency, decimal> { [Currency.CAD] = 100_000m },
            new ClosePriceOrderPriceCalculationStrategy(),
            new DynamicWeightPositionSizer(Currency.CAD),
            new InverseVolatilityConstruction(),
            RebalancingFrequency.Daily,
            lookbackWindow: 10);

        strategy.GenerateSignals(s_start.AddDays(usdCad.Length - 1), Currency.CAD, marketData, fx);

        var weights = strategy.LastComputedWeights!;
        weights[s_xbb].Should().BeGreaterThan(weights[s_vgsh],
            "in CAD terms XBB barely moves and VGSH carries the USD/CAD volatility; on native prices VGSH would be the flat one");
        weights[s_xbb].Should().BeGreaterThan(0.9m, "inverse-volatility puts nearly everything on the near-riskless-in-CAD asset");
    }

    // ─── Risk rules value foreign positions in base currency ─────────────

    [Fact]
    public void MaxPositionSizeRule_ForeignAsset_ValuesThePositionInBase()
    {
        // 130,000 CAD portfolio; a 100-share VGSH buy at 100 USD is 10,000 USD = 13,000 CAD = 10.0% —
        // exactly at a 10% cap. In native terms it looks like 7.7% and would pass a 9% cap it must fail.
        var portfolio = CadPortfolioHoldingUsdAsset(totalEquityCad: 130_000m, usdCad: 1.30m);
        var order = new Order(s_start, "S", s_vgsh, TradeAction.Buy, OrderType.Market, 100);

        new MaxPositionSizeRule(0.09m).Evaluate(order, portfolio).IsAllowed.Should().BeFalse(
            "13,000 CAD of 130,000 is 10%, above 9%");
        new MaxPositionSizeRule(0.10m).Evaluate(order, portfolio).IsAllowed.Should().BeTrue(
            "10% is exactly the cap");
    }

    [Fact]
    public void MaxSectorExposureRule_ForeignAsset_ValuesTheExposureInBase()
    {
        var portfolio = CadPortfolioHoldingUsdAsset(totalEquityCad: 130_000m, usdCad: 1.30m);
        var order = new Order(s_start, "S", s_vgsh, TradeAction.Buy, OrderType.Market, 100);
        var classes = new Dictionary<Symbol, AssetClassCode> { [s_vgsh] = AssetClassCode.FixedIncome };

        new MaxSectorExposureRule(0.09m, classes).Evaluate(order, portfolio).IsAllowed.Should().BeFalse();
        new MaxSectorExposureRule(0.10m, classes).Evaluate(order, portfolio).IsAllowed.Should().BeTrue();
    }

    // ─── FxLeadDays: a carry-forward source for a first bar the FX source skipped ──

    [Fact]
    public async Task BuildAsync_FxLeadDays_RequestsFxHistoryBeforeTheFirstBar()
    {
        var pipeline = new RecordingPipeline();
        var builder = new BacktestDatasetBuilder(pipeline, NullLogger<BacktestDatasetBuilder>.Instance);

        await builder.BuildAsync(new BacktestDatasetSpec
        {
            Symbols = [],
            StartDate = new DateOnly(2024, 7, 1),
            EndDate = new DateOnly(2024, 7, 31),
            BaseCurrency = Currency.CAD,
            ForeignCurrencies = [Currency.USD],
            FxLeadDays = 10,
        });

        pipeline.FxRanges.Should().ContainSingle().Which.From.Should().Be(new DateOnly(2024, 6, 21),
            "the FX request starts FxLeadDays before the price window; the price window itself is unchanged");
    }

    [Fact]
    public async Task BuildAsync_NegativeFxLeadDays_Throws()
    {
        var builder = new BacktestDatasetBuilder(new RecordingPipeline(), NullLogger<BacktestDatasetBuilder>.Instance);

        var act = () => builder.BuildAsync(new BacktestDatasetSpec
        {
            Symbols = [],
            StartDate = new DateOnly(2024, 7, 1),
            EndDate = new DateOnly(2024, 7, 31),
            FxLeadDays = -1,
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static IPortfolio CadPortfolioHoldingUsdAsset(decimal totalEquityCad, decimal usdCad)
    {
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Positions).Returns(new Dictionary<Symbol, int>());

        var portfolio = new Mock<IPortfolio>();
        portfolio.Setup(p => p.BaseCurrency).Returns(Currency.CAD);
        portfolio.Setup(p => p.GetAssetCurrency(s_vgsh)).Returns(Currency.USD);
        portfolio.Setup(p => p.Strategies).Returns(new Dictionary<string, IStrategy> { ["S"] = strategy.Object });
        portfolio.Setup(p => p.EquityCurve).Returns(new SortedDictionary<DateOnly, decimal> { [s_start] = totalEquityCad });
        portfolio.Setup(p => p.CalculateTotalPortfolioValue(s_start)).Returns(totalEquityCad);
        portfolio.Setup(p => p.HistoricalMarketData).Returns(new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>
        {
            [s_start] = new SortedDictionary<Symbol, Bar> { [s_vgsh] = new Bar(s_start, 100m, 100m, 100m, 100m, 100m, 1_000) },
        });
        portfolio.Setup(p => p.HistoricalFxConversionRates).Returns(new SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>>
        {
            [s_start] = new SortedDictionary<Currency, decimal> { [Currency.USD] = usdCad },
        });
        return portfolio.Object;
    }

    /// <summary>Answers every request with an empty payload and records the FX ranges asked for.</summary>
    private sealed class RecordingPipeline : IDataPipeline
    {
        public List<DateRange> FxRanges { get; } = [];

        public Task<DataEnvelope<IReadOnlyList<TRecord>>> ExecuteAsync<TRequest, TRecord>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IDataRequest
        {
            if (request is FxHistoryRequest fx)
            {
                FxRanges.Add(fx.Range);
            }

            IReadOnlyList<TRecord> empty = [];
            return Task.FromResult(new DataEnvelope<IReadOnlyList<TRecord>>(empty, new DataCoverage(0, 0, 0, 0m), [], []));
        }

        public Task<IReadOnlyDictionary<TDatasetKey, DataEnvelope<IReadOnlyList<TRecord>>>> ExecutePartitionedAsync<TRequest, TRecord, TPartitionKey, TDatasetKey>(
            TRequest request, Func<TRequest, TPartitionKey, TRequest> splitForPartition, Func<TRequest, TPartitionKey, TDatasetKey> buildDatasetKey, CancellationToken cancellationToken = default)
            where TRequest : IBatchDataRequest<TPartitionKey>
            where TDatasetKey : notnull => throw new NotSupportedException();

        public Task<Boutquin.MarketData.Abstractions.Snapshots.IDataSnapshot> AcquireReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
