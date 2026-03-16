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
/// Tests for D2: Position.Sell book value arithmetic.
/// </summary>
public sealed class PositionTests
{
    /// <summary>
    /// D2: Selling 20 of 100 shares should reduce book value by exactly 20%, not 25%.
    /// Bug: original code does Quantity -= shares first, then divides by the new (reduced) Quantity.
    /// </summary>
    [Fact]
    public void Position_Sell_BookValueProportionalToPreSaleQuantity()
    {
        // Arrange — 100 shares with $1000 book value
        var position = new Position("AAPL", 100, 1000m);

        // Act — sell 20 shares at $15/share with $5 fee
        position.Sell(20, 15m, 5m);

        // Assert — book value should be reduced by 20% (proportion of pre-sale qty), minus fee
        // Expected: 1000 * (1 - 20/100) - 5 = 800 - 5 = 795
        position.BookValue.Should().Be(795m);
        position.Quantity.Should().Be(80);
    }

    /// <summary>
    /// D2: Selling all shares should result in zero book value (minus fee).
    /// This was a divide-by-zero with the old code since Quantity becomes 0 before division.
    /// </summary>
    [Fact]
    public void Position_Sell_AllShares_ZeroBookValue()
    {
        // Arrange
        var position = new Position("AAPL", 50, 500m);

        // Act — sell all 50 shares
        position.Sell(50, 10m, 0m);

        // Assert — all shares sold, book value should be 0
        position.Quantity.Should().Be(0);
        position.BookValue.Should().Be(0m);
    }

    /// <summary>
    /// BUG-I13: Selling 0 shares should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Position_Sell_ZeroShares_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var position = new Position("AAPL", 100, 1000m);

        // Act
        var act = () => position.Sell(0, 10m, 0m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
