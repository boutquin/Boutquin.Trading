﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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

public sealed class OrderEventHandler : IEventHandler
{
    private readonly IPortfolio _portfolio;

    public OrderEventHandler(IPortfolio portfolio)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        _portfolio = portfolio;
    }

    public async Task HandleEventAsync(IFinancialEvent eventObj)
    {
        var orderEvent = eventObj as OrderEvent 
            ?? throw new ArgumentException("Event must be of type OrderEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        if (await _portfolio.SubmitOrderAsync(orderEvent))
        {
            // Log success
        }
        // Log failure
    }
}
