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

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Represents a security price.
/// </summary>
public sealed class SecurityPrice
{
    /// <summary>
    /// Gets the identifier of the security price.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the security identifier.
    /// </summary>
    public int SecurityId { get; }

    /// <summary>
    /// Gets the trade date.
    /// </summary>
    public DateTime TradeDate { get; }

    /// <summary>
    /// Gets the open price.
    /// </summary>
    public decimal OpenPrice { get; }

    /// <summary>
    /// Gets the high price.
    /// </summary>
    public decimal HighPrice { get; }

    /// <summary>
    /// Gets the low price.
    /// </summary>
    public decimal LowPrice { get; }

    /// <summary>
    /// Gets the close price.
    /// </summary>
    public decimal ClosePrice { get; }

    /// <summary>
    /// Gets the volume.
    /// </summary>
    public int Volume { get; }

    /// <summary>
    /// Gets the dividend.
    /// </summary>
    public decimal Dividend { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPrice"/> class.
    /// </summary>
    /// <param name="id">The identifier of the security price.</param>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <param name="openPrice">The open price.</param>
    /// <param name="highPrice">The high price.</param>
    /// <param name="lowPrice">The low price.</param>
    /// <param name="closePrice">The close price.</param>
    /// <param name="volume">The volume.</param>
    /// <param name="dividend">The dividend.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the id, securityId, volume is less than or equal to 0, or the openPrice, highPrice, lowPrice, closePrice, dividend is less than 0.</exception>
    public SecurityPrice(
        int id, 
        int securityId, 
        DateTime tradeDate, 
        decimal openPrice, 
        decimal highPrice, 
        decimal lowPrice, 
        decimal closePrice, 
        int volume, 
        decimal dividend)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than 0.");
        }

        if (securityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(securityId), "SecurityId must be greater than 0.");
        }

        if (openPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openPrice), "OpenPrice must be greater than or equal to 0.");
        }

        if (highPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(highPrice), "HighPrice must be greater than or equal to 0.");
        }
        if (lowPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lowPrice), "LowPrice must be greater than or equal to 0.");
        }

        if (closePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(closePrice), "ClosePrice must be greater than or equal to 0.");
        }

        if (volume <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be greater than 0.");
        }

        if (dividend < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dividend), "Dividend must be greater than or equal to 0.");
        }

        Id = id;
        SecurityId = securityId;
        TradeDate = tradeDate;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        Dividend = dividend;
    }
}
