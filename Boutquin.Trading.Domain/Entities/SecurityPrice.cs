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
using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Represents a security price.
/// </summary>
public sealed class SecurityPrice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPrice"/> class.
    /// </summary>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="openPrice">The open price.</param>
    /// <param name="highPrice">The high price.</param>
    /// <param name="lowPrice">The low price.</param>
    /// <param name="closePrice">The close price.</param>
    /// <param name="volume">The volume.</param>
    /// <param name="dividend">The dividend.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the id, securityId, volume is less than or equal to 0, or the openPrice, highPrice, lowPrice, closePrice, dividend is less than 0.</exception>
    public SecurityPrice(
        DateTime tradeDate,
        SecurityId securityId,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
        decimal dividend)
    {
        // Validate parameters
        Guard.AgainstNegativeOrZero((int)securityId, nameof(securityId));
        Guard.AgainstNegativeOrZero(openPrice, nameof(openPrice));
        Guard.AgainstNegativeOrZero(highPrice, nameof(highPrice));
        Guard.AgainstNegativeOrZero(lowPrice, nameof(lowPrice));
        Guard.AgainstNegativeOrZero(closePrice, nameof(closePrice));
        Guard.AgainstNegativeOrZero(volume, nameof(volume));
        Guard.AgainstNegative(dividend, nameof(dividend));

        TradeDate = tradeDate;
        SecurityId = securityId;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        Dividend = dividend;
    }

    /// <summary>
    /// The identifier of the security price.
    /// </summary>
    private int _id; // Private key for EF

        /// <summary>
    /// Gets the trade date.
    /// </summary>
    public DateTime TradeDate { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the security identifier.
    /// </summary>
    public SecurityId SecurityId { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the open price.
    /// </summary>
    public decimal OpenPrice { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the high price.
    /// </summary>
    public decimal HighPrice { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the low price.
    /// </summary>
    public decimal LowPrice { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the close price.
    /// </summary>
    public decimal ClosePrice { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the volume.
    /// </summary>
    public int Volume { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the dividend.
    /// </summary>
    public decimal Dividend { get; private set; } // Setter is for EF

    /// <summary>
    /// The name of the primary key column in the SecurityPrice table.
    /// </summary>
    public const string SecurityPrice_Key_Name = nameof(SecurityPrice._id);
}
