// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Domain.Interfaces;

using Enums;

/// <summary>
/// Classifies the current economic regime based on macro indicator signals.
/// </summary>
public interface IRegimeClassifier
{
    /// <summary>
    /// Classifies the current economic regime.
    /// </summary>
    /// <param name="growthSignal">The growth indicator value (positive = rising growth).</param>
    /// <param name="inflationSignal">The inflation indicator value (positive = rising inflation).</param>
    /// <returns>The classified economic regime.</returns>
    EconomicRegime Classify(decimal growthSignal, decimal inflationSignal);
}
