// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.DataAccess.Entities;

/// <summary>
/// Represents a trading position with a symbol, quantity, book value, and market value.
/// </summary>
public sealed class Position
{
    /// <summary>
    /// Gets the symbol of the trading position.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the quantity of shares in the trading position.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Gets the book value of the trading position.
    /// </summary>
    public decimal BookValue { get; private set; }

    /// <summary>
    /// Gets the market value of the trading position.
    /// </summary>
    public decimal MarketValue { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Position"/> class with the specified symbol, quantity, and book value.
    /// </summary>
    /// <param name="symbol">The symbol of the trading position.</param>
    /// <param name="quantity">The quantity of shares in the trading position.</param>
    /// <param name="bookValue">The book value of the trading position.</param>
    public Position(
        string symbol, 
        int quantity, 
        decimal bookValue)
    {
        Guard.AgainstNullOrWhiteSpace(() => symbol);

        Symbol = symbol;
        Quantity = quantity;
        BookValue = bookValue;
        MarketValue = 0;
    }

    /// <summary>
    /// Buys additional shares for the position.
    /// </summary>
    /// <param name="shares">The number of shares to buy.</param>
    /// <param name="price">The price per share.</param>
    /// <param name="transactionFee">The transaction fee.</param>
    /// <exception cref="ArgumentException">Thrown if shares, price, or transactionFee is negative.</exception>
    public void Buy(
        int shares, 
        decimal price, 
        decimal transactionFee)
    {
        Guard.AgainstNegative(() => shares);
        Guard.AgainstNegative(() => price);
        Guard.AgainstNegative(() => transactionFee);

        Quantity += shares;
        BookValue += (shares * price) + transactionFee;
    }

    /// <summary>
    /// Sells shares from the position.
    /// </summary>
    /// <param name="shares">The number of shares to sell.</param>
    /// <param name="price">The price per share.</param>
    /// <param name="transactionFee">The transaction fee.</param>
    /// <exception cref="ArgumentException">Thrown if shares, price, or transactionFee is negative or if the number of shares to sell is greater than the current quantity.</exception>
    public void Sell(int shares, decimal price, decimal transactionFee)
    {
        Guard.AgainstNegative(() => shares);
        Guard.AgainstNegative(() => price);
        Guard.AgainstNegative(() => transactionFee);
        Guard.Against(shares > Quantity).With<ArgumentException>("Cannot sell more shares than the current quantity.");
        
        Quantity -= shares;
        var proportion = (decimal)shares / Quantity;
        var soldBookValue = BookValue * proportion;
        BookValue = BookValue - soldBookValue - transactionFee;
    }

    /// <summary>
    /// Updates the market value of the position.
    /// </summary>
    /// <param name="price">The current price per share.</param>
    /// <exception cref="ArgumentException">Thrown if the price is negative.</exception>
    public void UpdateMarketValue(decimal price)
    {
        Guard.AgainstNegative(() => price);

        MarketValue = Quantity * price;
    }
}
