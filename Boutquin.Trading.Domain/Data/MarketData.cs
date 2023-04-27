// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Data;

/// <summary>
/// The MarketData record encapsulates the data points for a financial
/// asset at a specific point in time, providing information about its
/// open, high, low, close prices, volume, and timestamp.
/// </summary>
/// <param name="Timestamp">The timestamp of the market data point,
/// represented as a DateTime object.
/// </param>
/// <param name="Asset">The name of the financial asset, represented
/// as a string.
/// </param>
/// <param name="Open">The opening price of the financial asset, represented
/// as a decimal value.
/// </param>
/// <param name="High">The highest price of the financial asset during the
/// market data point's time interval, represented as a decimal value.
/// </param>
/// <param name="Low">The lowest price of the financial asset during the
/// market data point's time interval, represented as a decimal value.
/// </param>
/// <param name="Close">The closing price of the financial asset, represented
/// as a decimal value.
/// </param>
/// <param name="Volume">The volume of the financial asset traded during
/// the market data point's time interval, represented as a long integer.
/// </param>
public record MarketData(
    DateOnly Timestamp,
    string Asset,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume) : IAssetData;
