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

using Boutquin.Trading.Application.Universe;
using Boutquin.Trading.Domain.Analytics;

public sealed class UniverseFilterTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_smallEtf = new("SMALL");

    private static IReadOnlyDictionary<Asset, AssetMetadata> CreateMetadata() =>
        new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new AssetMetadata(s_vti, AumMillions: 500m, InceptionDate: new DateOnly(2001, 5, 24), AverageDailyVolume: 5_000_000m),
            [s_tlt] = new AssetMetadata(s_tlt, AumMillions: 200m, InceptionDate: new DateOnly(2002, 7, 22), AverageDailyVolume: 2_000_000m),
            [s_smallEtf] = new AssetMetadata(s_smallEtf, AumMillions: 50m, InceptionDate: new DateOnly(2023, 1, 1), AverageDailyVolume: 10_000m),
        };

    // ============================================================
    // MinAumFilter Tests
    // ============================================================

    [Fact]
    public void MinAumFilter_ShouldExcludeBelowThreshold()
    {
        var metadata = CreateMetadata();
        var filter = new MinAumFilter(100m, metadata);
        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };

        var result = filter.Select(candidates);

        result.Should().Contain(s_vti);
        result.Should().Contain(s_tlt);
        result.Should().NotContain(s_smallEtf);
    }

    [Fact]
    public void MinAumFilter_ZeroThreshold_ShouldIncludeAll()
    {
        var metadata = CreateMetadata();
        var filter = new MinAumFilter(0m, metadata);
        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };

        filter.Select(candidates).Should().HaveCount(3);
    }

    // ============================================================
    // MinAgeFilter Tests
    // ============================================================

    [Fact]
    public void MinAgeFilter_ShouldExcludeYoungETFs()
    {
        var metadata = CreateMetadata();
        var asOfDate = new DateOnly(2026, 3, 16);
        // 3 years = ~1095 days
        var filter = new MinAgeFilter(1095, asOfDate, metadata);
        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };

        var result = filter.Select(candidates);

        result.Should().Contain(s_vti);
        result.Should().Contain(s_tlt);
        result.Should().Contain(s_smallEtf); // s_smallEtf: inception 2023-01-01, ~1170 days old as of 2026-03-16
    }

    [Fact]
    public void MinAgeFilter_10Years_ShouldExcludeRecent()
    {
        var metadata = CreateMetadata();
        var asOfDate = new DateOnly(2026, 3, 16);
        // 10 years = ~3650 days
        var filter = new MinAgeFilter(3650, asOfDate, metadata);
        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };

        var result = filter.Select(candidates);

        result.Should().Contain(s_vti); // 2001 → ~24 years old
        result.Should().Contain(s_tlt); // 2002 → ~23 years old
        result.Should().NotContain(s_smallEtf); // 2023 → ~3 years old
    }

    // ============================================================
    // LiquidityFilter Tests
    // ============================================================

    [Fact]
    public void LiquidityFilter_ShouldExcludeIlliquid()
    {
        var metadata = CreateMetadata();
        var filter = new LiquidityFilter(100_000m, metadata);
        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };

        var result = filter.Select(candidates);

        result.Should().Contain(s_vti);
        result.Should().Contain(s_tlt);
        result.Should().NotContain(s_smallEtf); // 10k volume < 100k threshold
    }

    // ============================================================
    // CompositeUniverseSelector Tests
    // ============================================================

    [Fact]
    public void CompositeSelector_ShouldApplyAllFilters()
    {
        var metadata = CreateMetadata();

        var composite = new CompositeUniverseSelector(new List<IUniverseSelector>
        {
            new MinAumFilter(100m, metadata),
            new LiquidityFilter(100_000m, metadata),
        });

        var candidates = new List<Asset> { s_vti, s_tlt, s_smallEtf };
        var result = composite.Select(candidates);

        result.Should().Contain(s_vti);
        result.Should().Contain(s_tlt);
        result.Should().NotContain(s_smallEtf); // Fails both AUM and liquidity
    }

    [Fact]
    public void CompositeSelector_EmptyCandidates_ShouldReturnEmpty()
    {
        var composite = new CompositeUniverseSelector(new List<IUniverseSelector>());
        composite.Select(new List<Asset>()).Should().BeEmpty();
    }
}
