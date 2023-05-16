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
namespace Boutquin.Trading.Domain.Events;

using Interfaces;

public sealed class EventProcessor
{
    private readonly Dictionary<Type, IEventHandler> _handlers;

    public EventProcessor(Dictionary<Type, IEventHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task ProcessEventAsync(IEvent eventObj)
    {
        if (_handlers.TryGetValue(eventObj.GetType(), out var handler))
        {
            await handler.HandleEventAsync(eventObj);
        }
        else
        {
            throw new NotSupportedException($"Unsupported event type: {eventObj.GetType()}");
        }
    }
}
