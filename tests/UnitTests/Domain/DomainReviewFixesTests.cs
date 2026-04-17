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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// TDD tests for domain review fixes (spec: review-fixes-domain).
/// Each nested class targets one finding from the deep code review.
/// </summary>
public sealed class DomainReviewFixesTests
{

    // ══════════════════════════════════════════════════════════════════
    // M8 — AnnualizedReturn wipeout guard
    // ══════════════════════════════════════════════════════════════════

    public sealed class M8Tests
    {
        [Fact]
        public void AnnualizedReturn_WithTotalWipeout_ThrowsCalculationException()
        {
            // Arrange — a single return of -100% produces cumulative return of -1.0
            var dailyReturns = new[] { -1.0m };

            // Act
            var act = () => dailyReturns.AnnualizedReturn();

            // Assert
            act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        }

        [Fact]
        public void AnnualizedReturn_WithWorseThanWipeout_ThrowsCalculationException()
        {
            // Arrange — a return of -1.5 produces cumulative return < -1
            var dailyReturns = new[] { -1.5m };

            // Act
            var act = () => dailyReturns.AnnualizedReturn();

            // Assert
            act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M12 — VaR confidenceLevel bounds
    // ══════════════════════════════════════════════════════════════════

    public sealed class M12Tests
    {
        private static readonly decimal[] s_sampleReturns = [0.01m, -0.02m, 0.005m, -0.01m, 0.015m];

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void HistoricalVaR_InvalidConfidenceLevel_ThrowsArgumentOutOfRangeException(double cl)
        {
            var act = () => s_sampleReturns.HistoricalVaR((decimal)cl);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void ParametricVaR_InvalidConfidenceLevel_ThrowsArgumentOutOfRangeException(double cl)
        {
            var act = () => s_sampleReturns.ParametricVaR((decimal)cl);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M13 — CsvMarketDataStorage removed — market data ingestion
    // is now in Boutquin.MarketData. Test removed.
    // ══════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════
    // M23 — RiskEvaluation.Rejected reason validation
    // ══════════════════════════════════════════════════════════════════

    public sealed class M23Tests
    {
        [Fact]
        public void RiskEvaluation_Rejected_NullReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected(null!);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_EmptyReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_WhitespaceReason_ThrowsArgumentException()
        {
            var act = () => RiskEvaluation.Rejected("   ");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RiskEvaluation_Rejected_ValidReason_Succeeds()
        {
            var result = RiskEvaluation.Rejected("drawdown exceeded");
            result.IsAllowed.Should().BeFalse();
            result.RejectionReason.Should().Be("drawdown exceeded");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // L2 — AdjustForSplit removed — Bar is an immutable record;
    // split adjustments are handled externally. Test removed.

    // ══════════════════════════════════════════════════════════════════
    // L8 — MarketDataProcessor removed — market data ingestion
    // is now in Boutquin.MarketData. Test removed.
    // ══════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════
    // H1 — CsvMarketDataStorage removed — market data ingestion
    // is now in Boutquin.MarketData. Test removed.
    // ══════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════
    // M21 — BrinsonFachlerResult uses Symbol keys
    // ══════════════════════════════════════════════════════════════════

    public sealed class M21Tests
    {
        [Fact]
        public void BrinsonFachlerResult_UsesAssetKeys()
        {
            var asset = new Symbol("Equity");
            var effects = new Dictionary<Symbol, decimal> { { asset, 0.01m } };

            var result = new Boutquin.Trading.Domain.Analytics.BrinsonFachlerResult(
                0.01m, 0.02m, 0.003m, 0.033m,
                effects, effects, effects);

            result.AssetAllocationEffects.Should().ContainKey(asset);
            result.AssetAllocationEffects[asset].Should().Be(0.01m);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // L3 — CorrelationAnalysisResult uses Symbol for AssetNames
    // ══════════════════════════════════════════════════════════════════

    public sealed class L3Tests
    {
        [Fact]
        public void CorrelationAnalysisResult_UsesAssetForAssetNames()
        {
            var assets = new List<Symbol> { new("A"), new("B") };
            var matrix = new decimal[2, 2] { { 1m, 0.5m }, { 0.5m, 1m } };

            var result = new Boutquin.Trading.Domain.Analytics.CorrelationAnalysisResult(
                matrix, assets, 1.2m);

            result.AssetNames.Should().HaveCount(2);
            result.AssetNames[0].Should().Be(new Symbol("A"));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // M19 — Nullable inner market data removed
    // ══════════════════════════════════════════════════════════════════

    public sealed class M19Tests
    {
        [Fact]
        public void IPortfolio_HistoricalMarketData_IsNonNullable()
        {
            // Verify via reflection that the generic argument is non-nullable
            var prop = typeof(IPortfolio).GetProperty("HistoricalMarketData");
            prop.Should().NotBeNull();

            // The property type should be SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>
            var propType = prop!.PropertyType;
            var innerType = propType.GetGenericArguments()[1];
            innerType.Should().Be(typeof(SortedDictionary<Symbol, Bar>));
        }

        [Fact]
        public void MarketEvent_Data_IsNonNullable()
        {
            // MarketEvent.HistoricalMarketData should be non-nullable
            var prop = typeof(MarketEvent).GetProperty("HistoricalMarketData");
            prop.Should().NotBeNull();

            var propType = prop!.PropertyType;
            propType.Should().Be(typeof(SortedDictionary<Symbol, Bar>));
        }
    }
}
