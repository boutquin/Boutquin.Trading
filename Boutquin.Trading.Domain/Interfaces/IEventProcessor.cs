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
/// The IEventProcessor interface defines a method for processing financial events.
/// This interface is used by classes that need to process different types of financial events.
/// </summary>
/// <remarks>
/// The IEventProcessor interface should be implemented by classes that process financial events.
/// The ProcessEventAsync method should be implemented to process the event and perform any necessary actions.
/// 
/// Here is an example of how to implement this interface:
/// <code>
/// public class EventProcessor : IEventProcessor
/// {
///     private readonly IReadOnlyDictionary&lt;Type, IEventHandler&gt; _handlers;
/// 
///     public EventProcessor(IReadOnlyDictionary&lt;Type, IEventHandler&gt; handlers)
///     {
///         _handlers = handlers;
///     }
/// 
///     public async Task ProcessEventAsync(IFinancialEvent eventObj)
///     {
///         if (_handlers.TryGetValue(eventObj.GetType(), out var handler))
///         {
///             await handler.HandleEventAsync(eventObj);
///         }
///         else
///         {
///             throw new NotSupportedException($"Unsupported event type: {eventObj.GetType()}");
///         }
///     }
/// }
/// </code>
/// </remarks>
public interface IEventProcessor
{
    /// <summary>
    /// Processes the provided financial event.
    /// </summary>
    /// <param name="eventObj">The financial event to process.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The ProcessEventAsync method should be implemented to process the provided financial event and perform any necessary actions.
    /// The specific actions to be performed will depend on the type of the event and the specific implementation of the IEventProcessor interface.
    /// </remarks>
    Task ProcessEventAsync(IFinancialEvent eventObj);
}
