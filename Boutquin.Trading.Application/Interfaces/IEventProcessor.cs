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

namespace Boutquin.Trading.Application.Interfaces;

/// <summary>
/// The IEventProcessor interface defines the contract for an event processor
/// responsible for managing a queue of events and processing them within
/// the context of a portfolio.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Enqueues an event object to the event queue.
    /// </summary>
    /// <param name="eventObj">The event object to be enqueued.</param>
    /// <remarks>
    /// The EnqueueEvent method should be implemented to add the event object to
    /// the event queue. The event object can be any object implementing the IEvent
    /// interface, such as market events, signal events, or order events.
    /// </remarks>
    /// <example>
    /// This is an example of how the EnqueueEvent method can be used:
    /// <code>
    /// IEventProcessor eventProcessor = new MyCustomEventProcessor();
    /// IEvent marketEvent = new MarketEvent("AAPL", new MarketData(...));
    /// eventProcessor.EnqueueEvent(marketEvent);
    /// </code>
    /// </example>
    void EnqueueEvent(IEvent eventObj);

    /// <summary>
    /// Processes the events in the event queue within the context of a given portfolio.
    /// </summary>
    /// <param name="portfolio">The portfolio object used to process the events.</param>
    /// <remarks>
    /// The ProcessEventQueue method should be implemented to iterate through the event
    /// queue and process each event within the context of the provided portfolio.
    /// </remarks>
    /// <example>
    /// This is an example of how the ProcessEventQueue method can be used:
    /// <code>
    /// IEventProcessor eventProcessor = new MyCustomEventProcessor();
    /// Portfolio myPortfolio = new Portfolio();
    /// eventProcessor.ProcessEventQueue(myPortfolio);
    /// </code>
    /// </example>
    void ProcessEventQueue(Portfolio portfolio);
}
