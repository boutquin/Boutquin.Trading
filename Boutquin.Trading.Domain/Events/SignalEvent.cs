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
namespace Boutquin.Trading.Domain.Events;

using ValueObjects;

/// <summary>
/// The SignalEvent record encapsulates the data points for a set of trading
/// signals generated by a specific strategy at a specific point in time,
/// providing information about the strategy name, timestamp, and the signals
/// for each asset.
/// </summary>
/// <param name="Timestamp">The timestamp of the signal event,
/// represented as a DateOnly object.
/// </param>
/// <param name="StrategyName">The name of the strategy generating the
/// signals, represented as a string.
/// </param>
/// <param name="Signals">A read-only dictionary containing the assets and
/// their corresponding SignalType generated by the strategy.
/// </param>
public sealed record SignalEvent(
    DateOnly Timestamp,
    string StrategyName,
    IReadOnlyDictionary<Asset, SignalType> Signals
) : IFinancialEvent;
