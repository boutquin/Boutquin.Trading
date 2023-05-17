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
namespace Boutquin.Trading.Domain.Events;

using Interfaces;

/// <summary>
/// The DividendEvent record encapsulates the data points for a dividend
/// event for a specific financial asset at a specific point in time,
/// providing information about the dividend per share, asset name, and
/// timestamp.
/// </summary>
/// <param name="Timestamp">The timestamp of the dividend event,
/// represented as a DateTime object.
/// </param>
/// <param name="Asset">The name of the financial asset associated
/// with the dividend event, represented as a string.
/// </param>
/// <param name="DividendPerShare">The dividend per share paid by the
/// financial asset during the dividend event, represented as a decimal
/// value.
/// </param>
public record DividendEvent(
    DateOnly Timestamp,
    string Asset,
    decimal DividendPerShare) : IEvent;
