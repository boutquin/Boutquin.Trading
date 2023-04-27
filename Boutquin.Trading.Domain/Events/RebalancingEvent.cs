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

using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Events;

/// <summary>
/// The RebalancingEvent record represents an event in which a strategy
/// updates the weightings of its assets to achieve the desired target weights.
/// </summary>
/// <param name="Timestamp">The timestamp of the rebalancing event,
/// represented as a DateTime object.
/// </param>
/// <param name="Assets">The list of assets associated with the rebalancing event,
/// represented as a list of strings.
/// </param>
/// <param name="TargetWeights">The list of target weights for the assets
/// associated with the rebalancing event, represented as a list of decimals.
/// </param>
public record RebalancingEvent(
    DateOnly Timestamp,
    List<string> Assets,
    List<decimal> TargetWeights) : IEvent;
