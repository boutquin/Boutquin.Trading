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

public sealed class SupersessionFilterTests
{
    private static readonly Asset s_schd = new("SCHD");
    private static readonly Asset s_jepi = new("JEPI");
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_spy = new("SPY");

    private static AssetMetadata Meta(Asset asset, Asset? supersededBy = null) =>
        new(asset, AumMillions: 1000m, InceptionDate: new DateOnly(2010, 1, 1),
            AverageDailyVolume: 1_000_000m, SupersededBy: supersededBy);

    // ============================================================
    // Basic supersession
    // ============================================================

    [Fact]
    public void Select_SupersededAssetWithReplacementPresent_ShouldExclude()
    {
        // SCHD superseded by JEPI; JEPI is in the candidate set → SCHD excluded
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_schd] = Meta(s_schd, supersededBy: s_jepi),
            [s_jepi] = Meta(s_jepi),
            [s_vti] = Meta(s_vti),
        };

        var sut = new SupersessionFilter(metadata);
        var candidates = new List<Asset> { s_schd, s_jepi, s_vti };
        var result = sut.Select(candidates);

        result.Should().NotContain(s_schd);
        result.Should().Contain(s_jepi);
        result.Should().Contain(s_vti);
    }

    [Fact]
    public void Select_SupersededAssetWithReplacementAbsent_ShouldInclude()
    {
        // SCHD superseded by JEPI, but JEPI not in candidates → SCHD stays
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_schd] = Meta(s_schd, supersededBy: s_jepi),
            [s_vti] = Meta(s_vti),
        };

        var sut = new SupersessionFilter(metadata);
        var candidates = new List<Asset> { s_schd, s_vti };
        var result = sut.Select(candidates);

        result.Should().Contain(s_schd);
        result.Should().Contain(s_vti);
    }

    [Fact]
    public void Select_NoSupersession_ShouldReturnAll()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = Meta(s_vti),
            [s_spy] = Meta(s_spy),
        };

        var sut = new SupersessionFilter(metadata);
        var candidates = new List<Asset> { s_vti, s_spy };
        var result = sut.Select(candidates);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Select_AssetNotInMetadata_ShouldInclude()
    {
        // Unknown asset has no metadata — should pass through
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = Meta(s_vti),
        };

        var sut = new SupersessionFilter(metadata);
        var unknown = new Asset("UNKNOWN");
        var result = sut.Select(new List<Asset> { s_vti, unknown });

        result.Should().Contain(unknown);
    }

    [Fact]
    public void Select_EmptyCandidates_ShouldReturnEmpty()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>();
        var sut = new SupersessionFilter(metadata);
        var result = sut.Select(new List<Asset>());

        result.Should().BeEmpty();
    }
}
