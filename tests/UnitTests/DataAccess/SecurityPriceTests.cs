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

namespace Boutquin.Trading.Tests.UnitTests.DataAccess;

/// <summary>
/// Tests for M11: SecurityPrice.Volume should be long, not int.
/// </summary>
public sealed class SecurityPriceTests
{
    [Fact]
    public void SecurityPrice_Volume_AcceptsLongValues()
    {
        // Arrange — volume exceeds int.MaxValue (2,147,483,647)
        var price = new SecurityPrice(
            new DateOnly(2024, 1, 15),
            securityId: 1,
            openPrice: 150m,
            highPrice: 155m,
            lowPrice: 145m,
            closePrice: 152m,
            volume: 3_000_000_000L,
            dividend: 0m);

        // Assert
        price.Volume.Should().Be(3_000_000_000L);
    }

    [Fact]
    public void SecurityPrice_Volume_MatchesDomainType()
    {
        // Verify via reflection that Volume property type is long (not int)
        var volumeProperty = typeof(SecurityPrice).GetProperty(nameof(SecurityPrice.Volume));
        volumeProperty.Should().NotBeNull();
        volumeProperty!.PropertyType.Should().Be(typeof(long));
    }
}
