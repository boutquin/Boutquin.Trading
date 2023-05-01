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

using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Events;

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The IStrategy interface defines the contract for a trading strategy,
/// which can receive market events, generate signals, place orders, and
/// receive fill and dividend events.
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// The name of the strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A dictionary containing the financial assets associated with the strategy
    /// and their respective positions.
    /// </summary>
    /// <remarks>
    /// The key is the asset symbol (e.g., "AAPL") and the value is the quantity
    /// of the asset in the strategy's position.
    /// </remarks>
    Dictionary<string, int> Positions { get; }

    /// <summary>
    /// The list of financial assets associated with the strategy.
    /// </summary>
    List<string> Assets { get => Positions.Keys.ToList(); } 

    /// <summary>
    /// This method is called by the trading engine whenever a new market
    /// event occurs for any of the assets associated with the strategy.
    /// The strategy should use this event to update its internal state and
    /// generate new signals as appropriate.
    /// </summary>
    /// <param name="marketData">The MarketData object representing the
    /// latest market event for an asset associated with the strategy.
    /// </param>
    void OnMarketEvent(MarketData marketData);

    /// <summary>
    /// This method is called by the trading engine whenever the strategy
    /// generates a new signal for an asset based on the latest market data.
    /// The strategy should use this method to perform any additional
    /// calculations or checks before generating an order event.
    /// </summary>
    /// <param name="marketData">The MarketData object representing the
    /// latest market event for the asset associated with the signal.
    /// </param>
    /// <param name="asset">The name of the financial asset associated
    /// with the signal.
    /// </param>
    /// <returns>A SignalEvent object representing the generated signal.
    /// </returns>
    SignalEvent OnSignalEvent(MarketData marketData, string asset);

    /// <summary>
    /// This method is called by the trading engine whenever the strategy
    /// generates a new order event based on the latest signal event.
    /// The strategy should use this method to perform any additional
    /// calculations or checks before placing the order.
    /// </summary>
    /// <param name="signalEvent">The SignalEvent object representing the
    /// signal associated with the order event.
    /// </param>
    /// <returns>An OrderEvent object representing the generated order.
    /// </returns>
    OrderEvent OnOrderEvent(SignalEvent signalEvent);

    /// <summary>
    /// This method is called by the trading engine whenever a fill event
    /// occurs for an order generated by the strategy.
    /// The strategy should use this method to update its internal state and
    /// perform any necessary calculations or checks.
    /// </summary>
    /// <param name="fillEvent">The FillEvent object representing the
    /// latest fill event for an asset associated with the strategy.
    /// </param>
    void OnFillEvent(FillEvent fillEvent);

    /// <summary>
    /// This method is called by the trading engine whenever a dividend event
    /// occurs for an asset associated with the strategy.
    /// The strategy should use this method to update its internal state and
    /// perform any necessary calculations or checks.
    /// </summary>
    /// <param name="dividendEvent">The DividendEvent object representing
    /// the latest dividend event for an asset associated with the strategy.
    /// </param>
    void OnDividendEvent(DividendEvent dividendEvent);

    /// <summary>
    /// This method is called by the trading engine whenever a new
    /// rebalancing event occurs for the strategy.
    /// The strategy should use this event to adjust the weightings of
    /// its assets as necessary to achieve the desired target weights.
    /// </summary>
    /// <param name="rebalancingEvent">The RebalancingEvent object
    /// representing the latest rebalancing event for the strategy.
    /// </param>
    void OnRebalancingEvent(RebalancingEvent rebalancingEvent);

    /// <summary>
    /// Calculates the current equity of the strategy, based on the current
    /// values of its positions and cash balance.
    /// </summary>
    /// <returns>
    /// A decimal value representing the current equity of the strategy.
    /// </returns>
    decimal CalculateEquity();

    /// <summary>
    /// The frequency at which the strategy should rebalance its assets.
    /// </summary>
    RebalancingFrequency RebalancingFrequency { get; }

    /// <summary>
    /// The position sizer used to determine the position size.
    /// </summary>
    IPositionSizer PositionSizer { get; }

    /// <summary>
    /// The slippage percentage used when placing orders.
    /// </summary>
    /// <remarks>
    /// Slippage is a measure of the difference between the expected price of a trade
    /// and the price at which the trade is executed. It is expressed as a percentage.
    /// </remarks>
    decimal Slippage { get; }

    /// <summary>
    /// The commission fee applied to each transaction.
    /// </summary>
    /// <remarks>
    /// The commission is a fixed amount charged for each transaction, regardless of
    /// the size of the order.
    /// </remarks>
    decimal Commission { get; }
}