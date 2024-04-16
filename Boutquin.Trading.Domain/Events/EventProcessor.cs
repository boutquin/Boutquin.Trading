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
namespace Boutquin.Trading.Domain.Events;

using Interfaces;

/// <summary>
/// Processes financial events by delegating to the appropriate event handler.
/// </summary>
/// <remarks>
/// This class is responsible for processing financial events. It does this by delegating the processing to the appropriate event handler.
/// The event handlers are provided to the EventProcessor via a dictionary mapping event types to their handlers.
/// 
/// Here is an example of how to use this class:
/// <code>
/// var handlers = new Dictionary&lt;Type, IEventHandler&gt;()
/// {
///     { typeof(OrderEvent), new OrderEventHandler() },
///     { typeof(MarketEvent), new MarketEventHandler() },
///     { typeof(FillEvent), new FillEventHandler() },
///     { typeof(SignalEvent), new SignalEventHandler() }
/// };
/// 
/// var eventProcessor = new EventProcessor(handlers);
/// 
/// var tradeEvent = new TradeEvent();
/// await eventProcessor.ProcessEventAsync(tradeEvent);
/// </code>
/// </remarks>
public sealed class EventProcessor(IPortfolio portfolio, IReadOnlyDictionary<Type, IEventHandler> handlers) : IEventProcessor
{
    /// <summary>
    /// Processes the provided financial event.
    /// </summary>
    /// <param name="eventObj">The financial event to process.</param>
    /// <exception cref="NotSupportedException">Thrown when there is no handler for the provided event type.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task ProcessEventAsync(IFinancialEvent eventObj)
    {
        if (handlers.TryGetValue(eventObj.GetType(), out var handler))
        {
            await handler.HandleEventAsync(portfolio, eventObj);
        }
        else
        {
            throw new NotSupportedException($"Unsupported event type: {eventObj.GetType()}");
        }
    }
}
