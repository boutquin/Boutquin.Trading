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

global using System.Collections.ObjectModel;

global using Boutquin.Trading.Application;
global using Boutquin.Trading.Application.Brokers;
global using Boutquin.Trading.Application.EventHandlers;
global using Boutquin.Trading.Application.PositionSizing;
global using Boutquin.Trading.Application.Strategies;
global using Boutquin.Trading.Data.AlphaVantage;
global using Boutquin.Trading.Domain.Data;
global using Boutquin.Trading.Domain.Enums;
global using Boutquin.Trading.Domain.Events;
global using Boutquin.Trading.Domain.Interfaces;
global using Boutquin.Trading.Domain.ValueObjects;
global using Boutquin.Trading.Sample;

global using Microsoft.Extensions.Caching.Distributed;
