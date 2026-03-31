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
    /// D2: Selling 20 of 100 shares should reduce book value by exactly 20%.
    /// </summary>
    [Fact]
    public void Position_Sell_BookValueProportionalToPreSaleQuantity()
    {
        // Arrange — 100 shares with $1000 book value
        var position = new Position("AAPL", 100, 1000m);

        // Act — sell 20 shares at $15/share with $5 fee
        position.Sell(20, 15m, 5m);

        // Assert — book value should be reduced by 20% (proportion of pre-sale qty)
        // M6 fix: fee is a realized cost, not a reduction of remaining book value
        // Expected: 1000 * (1 - 20/100) = 800
        position.BookValue.Should().Be(800m);
        position.Quantity.Should().Be(80);
    }

    /// <summary>
    /// D2: Selling all shares should result in zero book value.
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
    /// M6: Transaction fee should not reduce the book value of remaining shares.
    /// </summary>
    [Fact]
    public void Sell_TransactionFee_DoesNotReduceBookValue()
    {
        // Arrange — 100 shares at $10 = $1000 book value
        var position = new Position("TEST", 100, 1000m);

        // Act — sell 50 shares at $10 with $100 fee
        position.Sell(50, 10m, 100m);

        // Assert — remaining 50 shares should still have book value of $500
        position.BookValue.Should().Be(500m);
    }

    /// <summary>
    /// M6: Selling all shares zeroes book value regardless of fee.
    /// </summary>
    [Fact]
    public void Sell_AllShares_BookValueZero()
    {
        // Arrange
        var position = new Position("TEST", 100, 1000m);

        // Act — sell all with fee
        position.Sell(100, 10m, 50m);

        // Assert
        position.BookValue.Should().Be(0m);
    }

    /// <summary>
    /// M6: High fee should not make book value negative.
    /// </summary>
    [Fact]
    public void Sell_HighFee_DoesNotMakeBookValueNegative()
    {
        // Arrange — 10 shares at $10 = $100 book value
        var position = new Position("TEST", 10, 100m);

        // Act — sell 5 shares at $10 with $200 fee
        position.Sell(5, 10m, 200m);

        // Assert — remaining 5 shares should have book value of $50 (proportional), not negative
        position.BookValue.Should().Be(50m);
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
