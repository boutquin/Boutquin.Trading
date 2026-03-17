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
/// TDD tests verifying low-severity domain fixes.
/// </summary>
public sealed class LowSeverityDomainTests
{
    // ── BUG-D-LOW-01: StrategyName null/empty validation ────────────

    [Fact]
    public void StrategyName_Null_ThrowsArgumentException()
    {
        // Act
        var act = () => new StrategyName(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StrategyName_Empty_ThrowsArgumentException()
    {
        // Act
        var act = () => new StrategyName("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StrategyName_WhiteSpace_ThrowsArgumentException()
    {
        // Act
        var act = () => new StrategyName("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StrategyName_Valid_Succeeds()
    {
        // Act
        var name = new StrategyName("BuyAndHold");

        // Assert
        name.Value.Should().Be("BuyAndHold");
        name.ToString().Should().Be("BuyAndHold");
    }

    // ── BUG-D-LOW-02: SecurityId equality symmetry ──────────────────

    [Fact]
    public void SecurityId_Equality_IsSymmetric()
    {
        // Arrange
        var a = new SecurityId(42);
        var b = new SecurityId(42);
        var c = new SecurityId(99);

        // Assert — symmetric for equal values
        a.Equals(b).Should().Be(b.Equals(a));

        // Assert — symmetric for unequal values
        a.Equals(c).Should().Be(c.Equals(a));
    }

    [Fact]
    public void SecurityId_Equality_WithInt_IsConsistent()
    {
        // Arrange
        var id = new SecurityId(42);

        // Assert — SecurityId == int
        (id == 42).Should().BeTrue();
        (id != 42).Should().BeFalse();
        (id == 99).Should().BeFalse();
    }
}
