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

using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Events;

/// <summary>
/// The SignalEvent record encapsulates the data points for a signal event
/// for a specific financial asset at a specific point in time, providing
/// information about the signal type, asset name, and timestamp.
/// </summary>
/// <param name="Timestamp">The timestamp of the signal event, represented
/// as a DateTime object.
/// </param>
/// <param name="Asset">The name of the financial asset associated with
/// the signal event, represented as a string.
/// </param>
/// <param name="SignalType">The type of the signal event, represented
/// as a SignalType enum value.
/// </param>
public record SignalEvent(
    DateOnly Timestamp, 
    string Asset, 
    SignalType SignalType) : IEvent;
