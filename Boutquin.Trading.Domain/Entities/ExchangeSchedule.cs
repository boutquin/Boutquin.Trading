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


using Boutquin.Domain.Helpers;
using System.Xml.Linq;
using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Represents an exchange schedule.
/// </summary>
public sealed class ExchangeSchedule
{
    /// <summary>
    /// Gets the identifier of the exchange schedule.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the exchange code.
    /// </summary>
    public ExchangeCode ExchangeCode { get; }

    /// <summary>
    /// Gets the opening time of the exchange.
    /// </summary>
    public TimeSpan OpenTime { get; }

    /// <summary>
    /// Gets the closing time of the exchange.
    /// </summary>
    public TimeSpan CloseTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeSchedule"/> class.
    /// </summary>
    /// <param name="id">The identifier of the exchange schedule.</param>
    /// <param name="exchangeCode">The exchange code.</param>
    /// <param name="openTime">The opening time of the exchange.</param>
    /// <param name="closeTime">The closing time of the exchange.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the id is less than or equal to 0, the exchangeCode is not defined in the enumeration, or the openTime and closeTime are not valid time values.</exception>
    public ExchangeSchedule(
        int id, 
        ExchangeCode exchangeCode, 
        TimeSpan openTime, 
        TimeSpan closeTime)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero(id, nameof(id));
        Guard.AgainstUndefinedEnumValue(exchangeCode, nameof(exchangeCode));

        if (openTime < TimeSpan.Zero || openTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(openTime), "Open time must be within the range of a single day.");
        }

        if (closeTime < TimeSpan.Zero || closeTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(closeTime), "Close time must be within the range of a single day.");
        }

        Id = id;
        ExchangeCode = exchangeCode;
        OpenTime = openTime;
        CloseTime = closeTime;
    }
}
