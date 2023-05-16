﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Domain.Interfaces;

using Enums;
using Events;

public interface IPortfolio
{
    Task UpdateHistoricalDataAsync(MarketEvent marketEvent);

    Task AllocateCapitalAsync();

    Task<IEnumerable<SignalEvent>> GenerateSignalsAsync(MarketEvent marketEvent);

    Task UpdatePositionAsync(string strategyName, string asset, decimal quantity);

    Task UpdateCashAsync(string strategyName, string assetCurrency, decimal amount);

    Task UpdateDailyReturnAsync(string strategyName, string asset, DateOnly timestamp, decimal returnAmount);

    Task UpdateEquityCurveAsync(DateOnly timestamp);

    Task AdjustPositionForSplitAsync(string asset, double splitRatio);

    Task AdjustHistoricalDataForSplitAsync(string asset, double splitRatio);

    Task UpdateCashForDividendAsync(string asset, decimal dividendAmount);

    IStrategy GetStrategy(string strategyName);

    CurrencyCode GetAssetCurrency(string asset);

    decimal CalculateTotalPortfolioValue(DateOnly timestamp);
}
