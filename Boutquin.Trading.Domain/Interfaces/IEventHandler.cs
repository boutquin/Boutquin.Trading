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
namespace Boutquin.Trading.Domain.Interfaces;
/// <summary>
/// The IEventHandler interface defines a method for handling financial events.
/// This interface is used by the EventProcessor class to delegate the processing of financial events to the appropriate event handler.
/// </summary>
/// <remarks>
/// The IEventHandler interface should be implemented by classes that handle specific types of financial events.
/// The HandleEventAsync method should be implemented to process the event and perform any necessary actions.
/// 
/// Here is an example of how to implement this interface:
/// <code>
/// public class TradeEventHandler : IEventHandler
/// {
///     public async Task HandleEventAsync(IFinancialEvent eventObj)
///     {
///         var tradeEvent = eventObj as TradeEvent;
///         // Process the trade event...
///     }
/// }
/// </code>
/// </remarks>
public interface IEventHandler
{
    /// <summary>
    /// Handles the provided financial event.
    /// </summary>
    /// <param name="portfolio">The portfolio for which the event is relevant.</param>
    /// <param name="eventObj">The financial event to handle.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method should be implemented to process the provided financial event and perform any necessary actions.
    /// The specific actions to be performed will depend on the type of the event and the specific implementation of the IEventHandler interface.
    /// </remarks>
    Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj);
}
