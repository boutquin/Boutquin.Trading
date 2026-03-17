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

using Boutquin.Trading.Application.Analytics;

public sealed class CorrelationAnalyzerValidationTests
{
    private static readonly Asset s_a = new("A");

    [Fact]
    public void Analyze_NullAssetNames_ThrowsArgumentNullException()
    {
        var act = () => CorrelationAnalyzer.Analyze(
            null!, new[] { new[] { 0.01m, 0.02m } }, new[] { 1.0m });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Analyze_NullReturns_ThrowsArgumentNullException()
    {
        var act = () => CorrelationAnalyzer.Analyze(
            new List<Asset> { s_a }, null!, new[] { 1.0m });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Analyze_NullWeights_ThrowsArgumentNullException()
    {
        var act = () => CorrelationAnalyzer.Analyze(
            new List<Asset> { s_a }, new[] { new[] { 0.01m, 0.02m } }, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(CorrelationAnalyzerValidationTestData.DimensionMismatch_ReturnsVsAssets),
        MemberType = typeof(CorrelationAnalyzerValidationTestData))]
    public void Analyze_DimensionMismatch_ReturnsVsAssets_ThrowsArgumentException(
        IReadOnlyList<Asset> assetNames, decimal[][] returns, decimal[] weights)
    {
        var act = () => CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(CorrelationAnalyzerValidationTestData.DimensionMismatch_WeightsVsAssets),
        MemberType = typeof(CorrelationAnalyzerValidationTestData))]
    public void Analyze_DimensionMismatch_WeightsVsAssets_ThrowsArgumentException(
        IReadOnlyList<Asset> assetNames, decimal[][] returns, decimal[] weights)
    {
        var act = () => CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(CorrelationAnalyzerValidationTestData.InsufficientObservations),
        MemberType = typeof(CorrelationAnalyzerValidationTestData))]
    public void Analyze_InsufficientObservations_ThrowsArgumentException(
        IReadOnlyList<Asset> assetNames, decimal[][] returns, decimal[] weights)
    {
        var act = () => CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        act.Should().Throw<ArgumentException>();
    }
}
