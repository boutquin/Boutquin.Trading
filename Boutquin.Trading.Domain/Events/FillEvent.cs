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

namespace Boutquin.Trading.Domain.Events;

/// <summary>
/// The FillEvent record encapsulates the data points for a fill event
/// for a specific financial asset at a specific point in time, providing
/// information about the fill price, asset name, quantity, commission,
/// strategy name, and timestamp.
/// </summary>
/// <param name="Timestamp">The timestamp of the fill event, represented
/// as a DateTime object.
/// </param>
/// <param name="Asset">The name of the financial asset associated with
/// the fill event, represented as a string.
/// </param>
/// <param name="Quantity">The quantity of the asset associated with the
/// fill event, represented as an integer value.
/// </param>
/// <param name="FillPrice">The fill price of the fill event, represented
/// as a decimal value.
/// </param>
/// <param name="Commission">The commission of the fill event, represented
/// as a decimal value.
/// </param>
/// <param name="StrategyName">The name of the strategy associated with
/// the fill event, represented as a string.
/// </param>
public record FillEvent(
    DateOnly Timestamp, 
    string Asset, 
    int Quantity, 
    decimal FillPrice, 
    decimal Commission, 
    string StrategyName) : IEvent;
