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

namespace Boutquin.Trading.Domain.ValueObjects;

/// <summary>
/// Represents a strategy name for a trading strategy.
/// </summary>
/// <remarks>
/// A strategy name is a unique identifier used to distinguish between different trading strategies. 
/// A strategy name may consist of letters, numbers or a combination of both.
/// 
/// <code>
/// // Example usage:
/// var strategyName = new StrategyName("TestStrategy");
/// Console.WriteLine(strategyName);  // Outputs: TestStrategy
/// </code>
/// </remarks>
public readonly record struct StrategyName(string Value)
{
    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    /// <remarks>
    /// This method overrides the base implementation to return the Value of the StrategyName.
    /// </remarks>
    public override string ToString() => Value;
}
