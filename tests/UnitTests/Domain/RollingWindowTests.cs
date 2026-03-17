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

using Boutquin.Trading.Domain.Helpers;
using FluentAssertions;

/// <summary>
/// Tests for the <see cref="RollingWindow{T}"/> class.
/// </summary>
public sealed class RollingWindowTests
{
    [Fact]
    public void Constructor_WithZeroCapacity_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => new RollingWindow<decimal>(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => new RollingWindow<decimal>(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsFull_BeforeCapacityReached_ShouldReturnFalse()
    {
        // Arrange
        var window = new RollingWindow<decimal>(60);

        // Act
        for (var i = 0; i < 59; i++)
        {
            window.Add(i);
        }

        // Assert
        window.IsFull.Should().BeFalse();
        window.Count.Should().Be(59);
    }

    [Fact]
    public void IsFull_AtCapacity_ShouldReturnTrue()
    {
        // Arrange
        var window = new RollingWindow<decimal>(60);

        // Act
        for (var i = 0; i < 60; i++)
        {
            window.Add(i);
        }

        // Assert
        window.IsFull.Should().BeTrue();
        window.Count.Should().Be(60);
    }

    [Fact]
    public void Add_BeyondCapacity_ShouldDropOldest()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);

        // Act
        window.Add(4m);

        // Assert
        window.Count.Should().Be(3);
        window.IsFull.Should().BeTrue();
        var array = window.ToArray();
        array.Should().Equal(2m, 3m, 4m);
    }

    [Fact]
    public void ToArray_ShouldReturnChronologicalOrder()
    {
        // Arrange
        var window = new RollingWindow<decimal>(5);
        window.Add(10m);
        window.Add(20m);
        window.Add(30m);
        window.Add(40m);
        window.Add(50m);

        // Act
        var array = window.ToArray();

        // Assert
        array.Should().Equal(10m, 20m, 30m, 40m, 50m);
    }

    [Fact]
    public void ToArray_AfterWraparound_ShouldReturnChronologicalOrder()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);
        window.Add(4m);
        window.Add(5m);

        // Act
        var array = window.ToArray();

        // Assert
        array.Should().Equal(3m, 4m, 5m);
    }

    [Fact]
    public void Capacity_ShouldReturnConfiguredValue()
    {
        // Arrange & Act
        var window = new RollingWindow<decimal>(60);

        // Assert
        window.Capacity.Should().Be(60);
    }

    [Fact]
    public void Count_WhenEmpty_ShouldReturnZero()
    {
        // Arrange & Act
        var window = new RollingWindow<decimal>(10);

        // Assert
        window.Count.Should().Be(0);
    }

    [Fact]
    public void ToArray_WhenNotFull_ShouldReturnOnlyAddedElements()
    {
        // Arrange
        var window = new RollingWindow<decimal>(10);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);

        // Act
        var array = window.ToArray();

        // Assert
        array.Should().Equal(1m, 2m, 3m);
    }

    [Fact]
    public void Indexer_ShouldReturnCorrectElement()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(10m);
        window.Add(20m);
        window.Add(30m);

        // Assert — index 0 is oldest
        window[0].Should().Be(10m);
        window[1].Should().Be(20m);
        window[2].Should().Be(30m);
    }

    [Fact]
    public void Indexer_AfterWraparound_ShouldReturnCorrectElement()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);
        window.Add(4m);

        // Assert
        window[0].Should().Be(2m);
        window[1].Should().Be(3m);
        window[2].Should().Be(4m);
    }

    [Fact]
    public void Indexer_OutOfRange_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);

        // Act
        var act = () => window[5];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Clear_ShouldResetWindow()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);

        // Act
        window.Clear();

        // Assert
        window.Count.Should().Be(0);
        window.IsFull.Should().BeFalse();
    }

    [Fact]
    public void Enumerable_ShouldIterateInChronologicalOrder()
    {
        // Arrange
        var window = new RollingWindow<decimal>(3);
        window.Add(1m);
        window.Add(2m);
        window.Add(3m);
        window.Add(4m);

        // Act
        var items = window.ToList();

        // Assert
        items.Should().Equal(2m, 3m, 4m);
    }
}
