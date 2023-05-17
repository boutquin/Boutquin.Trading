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
namespace Boutquin.Trading.Domain.Interfaces;

using Events;

/// <summary>
/// The IBrokerage interface represents the brokerage component in the
/// trading platform, providing methods for managing orders and
/// updating positions, as well as fetching account and market data.
/// This interface can be implemented by different classes for both
/// simulation and live trading scenarios.
/// </summary>
public interface IBrokerage
{
    /// <summary>
    /// Submits an order for execution by the brokerage. The method should
    /// handle different order types (e.g. Market, Limit, Stop, StopLimit).
    /// </summary>
    /// <param name="order">The order to be executed, represented as an Order object.</param>
    /// <returns>A boolean indicating whether the order submission was successful.</returns>
    Task<bool> SubmitOrderAsync(Order order);

    /// <summary>
    /// An event that is raised when an order is filled, providing the
    /// FillEvent data to the subscribers.
    /// </summary>
    event EventHandler<FillEvent> FillOccurred;
}
