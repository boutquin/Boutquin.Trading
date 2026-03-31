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

/// <summary>
/// A portfolio construction model whose target weights may not sum to 1.0.
/// Models implementing this interface intentionally scale weights for leverage or de-leverage
/// (e.g., volatility targeting). Consumers that validate weight sums should check for this
/// interface to allow weights greater than 1.0.
/// </summary>
/// <remarks>
/// <see cref="IPortfolioConstructionModel"/> documents that weights sum to 1.0.
/// <see cref="ILeveragedConstructionModel"/> relaxes that constraint for models
/// where leverage is the intended behavior.
/// </remarks>
public interface ILeveragedConstructionModel : IPortfolioConstructionModel;
