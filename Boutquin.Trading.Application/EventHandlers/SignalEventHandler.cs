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
namespace Boutquin.Trading.Application.EventHandlers;

public sealed class SignalEventHandler : IEventHandler
{
    private readonly IPortfolio _portfolio;

    public SignalEventHandler(IPortfolio portfolio)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        _portfolio = portfolio;
    }

    public async Task HandleEventAsync(IEvent eventObj)
    {
        var signalEvent = eventObj as SignalEvent 
            ?? throw new ArgumentException("Event must be of type SignalEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        var strategy = _portfolio.GetStrategy(signalEvent.StrategyName);

        // Compute the position sizes for the assets based on the signals.
        var positionSizes = strategy.PositionSizer.ComputePositionSizes(
            signalEvent.Timestamp,
            signalEvent.Signals,
            strategy,
            _portfolio.HistoricalMarketData,
            _portfolio.HistoricalFxConversionRates);

        // Iterate through the assets and generate OrderEvents for each asset.
        foreach (var asset in signalEvent.Signals.Keys)
        {
            // Get the desired position size for the current asset.
            var desiredPositionSize = positionSizes[asset];

            // Get the current position size for the current asset.
            var currentPositionSize = strategy.Positions.GetValueOrDefault(asset, 0);

            // Calculate the order size based on the difference between the desired position size and the current position size.
            var orderSize = desiredPositionSize - currentPositionSize;

            // If the order size is zero, no order needs to be placed.
            if (orderSize == 0)
            {
                continue;
            }

            // Determine the trade action based on the order size.
            var tradeAction = orderSize > 0 ? TradeAction.Buy : TradeAction.Sell;

            // Calculate the order prices for the current asset.
            var (orderType, primaryPrice, secondaryPrice) = 
                strategy.OrderPriceCalculationStrategy.CalculateOrderPrices(
                    signalEvent.Timestamp,
                    asset,
                    tradeAction,
                    _portfolio.HistoricalMarketData);

            // Create an OrderEvent for the current asset.
            var orderEvent = new OrderEvent(
                signalEvent.Timestamp,
                signalEvent.StrategyName,
                asset,
                tradeAction,
                orderType,
                Math.Abs(orderSize),
                primaryPrice,
                secondaryPrice);

            // Pass the OrderEvent to the EventProcessor for further processing.
            await _portfolio.EventProcessor.ProcessEventAsync(orderEvent);
        }
    }
}
