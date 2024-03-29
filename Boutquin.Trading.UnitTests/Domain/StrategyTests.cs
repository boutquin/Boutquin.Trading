﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.Tests.UnitTests.Domain;

using Boutquin.Domain.Exceptions;

using Helpers;

using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Interfaces;

public class StrategyTests
{
    [Fact]
    public void ComputeTotalValue_ShouldComputeCorrectly()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var timestamp = DateOnly.FromDateTime(DateTime.Today);
        var baseCurrency = CurrencyCode.USD;
        var marketData = new SortedDictionary<string, MarketData>
        {
            { "AAPL",
                new MarketData(
                    Timestamp: timestamp,
                    Open: 100,
                    High: 200,
                    Low: 50,
                    Close: 150,
                    AdjustedClose: 150,
                    Volume: 1000000,
                    DividendPerShare: 0,
                    SplitCoefficient: 1)
            }
        };
        var fxRates = new SortedDictionary<CurrencyCode, decimal>
        {
            { CurrencyCode.EUR, 0.85m }
        };

        strategy.Positions["AAPL"] = 10;
        ((TestStrategy)strategy).Assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };
        strategy.Cash[CurrencyCode.USD] = 1000;

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>> { { timestamp, marketData } };
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> { { timestamp, fxRates } };

        // Act
        var totalValue = strategy.ComputeTotalValue(timestamp, baseCurrency, historicalMarketData, historicalFxConversionRates);

        // Assert
        totalValue.Should().Be(2500);
    }

    [Fact]
    public void ComputeTotalValue_ShouldThrowException_WhenMarketDataNotFound()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var baseCurrency = CurrencyCode.USD;
        var fxRates = new SortedDictionary<CurrencyCode, decimal>
        {
            { CurrencyCode.EUR, 0.85m }
        };

        strategy.Positions["AAPL"] = 10;
        ((TestStrategy)strategy).Assets = new Dictionary<string, CurrencyCode> { { "AAPL", CurrencyCode.USD } };

        var historicalMarketData = new Dictionary<DateOnly, SortedDictionary<string, MarketData>>();
        var historicalFxConversionRates = new Dictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> { { date, fxRates } };

        // Act
        Action act = () => strategy.ComputeTotalValue(date, baseCurrency, historicalMarketData, historicalFxConversionRates);

        // Assert
        act.Should().Throw<EmptyOrNullDictionaryException>()
            .WithMessage("Parameter 'historicalMarketData' cannot be null or an empty dictionary.");
    }

    [Fact]
    public void UpdateCash_ShouldUpdateCorrectly()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var currency = CurrencyCode.USD;

        // Act
        strategy.UpdateCash(currency, 1000);

        // Assert
        strategy.Cash[currency].Should().Be(1000);
    }

    [Fact]
    public void UpdateCash_ShouldThrowException_WhenInvalidCurrency()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var currency = (CurrencyCode)999;

        // Act
        var act = () => strategy.UpdateCash(currency, 1000);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("Parameter 'currency' has an undefined value '999' for enum 'CurrencyCode'. (Parameter 'currency')");
    }

    [Fact]
    public void UpdatePositions_ShouldUpdateCorrectly()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var asset = "AAPL";

        // Act
        strategy.UpdatePositions(asset, 10);

        // Assert
        strategy.Positions[asset].Should().Be(10);
    }

    [Fact]
    public void UpdatePositions_ShouldThrowException_WhenInvalidAsset()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var asset = "   ";

        // Act
        var act = () => strategy.UpdatePositions(asset, 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Parameter 'asset' cannot be null, empty or contain only white-space characters. (Parameter 'asset')");
    }

    [Fact]
    public void GetPositionQuantity_ShouldReturnCorrectQuantity()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var asset = "AAPL";
        strategy.Positions[asset] = 10;

        // Act
        var quantity = strategy.GetPositionQuantity(asset);

        // Assert
        quantity.Should().Be(10);
    }

    [Fact]
    public void GetPositionQuantity_ShouldReturnZero_WhenAssetNotFound()
    {
        // Arrange
        IStrategy strategy = new TestStrategy();
        var asset = "AAPL";

        // Act
        var quantity = strategy.GetPositionQuantity(asset);

        // Assert
        quantity.Should().Be(0);
    }
}
