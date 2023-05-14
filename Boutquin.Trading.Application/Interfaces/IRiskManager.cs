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
/// The IRiskManager interface defines the contract for a risk management component
/// associated with a trading strategy. It is responsible for assessing the risk of
/// a strategy in a given portfolio and adjusting the capital allocation accordingly.
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// Assess the risk of a strategy within the context of a given portfolio.
    /// </summary>
    /// <param name="portfolio">The portfolio object containing the current positions and capital allocations.</param>
    /// <param name="strategy">The strategy object to be assessed for risk.</param>
    /// <returns>A boolean value indicating whether the risk level of the strategy is acceptable.</returns>
    /// <remarks>
    /// The AssessRisk method should be implemented to evaluate the risk associated with
    /// the given strategy in the context of the provided portfolio. The risk assessment
    /// can be based on various factors, such as volatility, maximum drawdown, position sizing,
    /// or other custom risk metrics. The method should return true if the risk is deemed acceptable
    /// and false otherwise.
    /// </remarks>
    /// <example>
    /// This is an example of how the AssessRisk method can be used:
    /// <code>
    /// IRiskManager riskManager = new MyCustomRiskManager();
    /// Portfolio myPortfolio = new Portfolio();
    /// IStrategy myStrategy = new MyCustomStrategy();
    /// bool isRiskAcceptable = riskManager.AssessRisk(myPortfolio, myStrategy);
    /// Console.WriteLine($"Is the risk acceptable? {isRiskAcceptable}");
    /// </code>
    /// </example>
    bool AssessRisk(Portfolio portfolio, IStrategy strategy);

    /// <summary>
    /// Adjust the capital allocation of a strategy within the context of a given portfolio.
    /// </summary>
    /// <param name="portfolio">The portfolio object containing the current positions and capital allocations.</param>
    /// <param name="strategy">The strategy object whose capital allocation needs to be adjusted.</param>
    /// <remarks>
    /// The AdjustCapitalAllocation method should be implemented to modify the capital allocated
    /// to a strategy based on the current risk assessment. This method can be used to increase
    /// or decrease the capital allocated to a strategy to maintain a desired risk level.
    /// </remarks>
    /// <example>
    /// This is an example of how the AdjustCapitalAllocation method can be used:
    /// <code>
    /// IRiskManager riskManager = new MyCustomRiskManager();
    /// Portfolio myPortfolio = new Portfolio();
    /// IStrategy myStrategy = new MyCustomStrategy();
    /// riskManager.AdjustCapitalAllocation(myPortfolio, myStrategy);
    /// Console.WriteLine($"New capital allocation for the strategy: {myStrategy.Capital}");
    /// </code>
    /// </example>
    void AdjustCapitalAllocation(Portfolio portfolio, IStrategy strategy);
}
