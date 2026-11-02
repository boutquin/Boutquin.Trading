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

namespace Boutquin.Trading.Application.PortfolioConstruction;

/// <summary>
/// Selects the distance metric used to build the single-linkage clustering
/// dendrogram in the Hierarchical Risk Parity (HRP) family of construction models.
/// </summary>
public enum ClusteringMethod
{
    /// <summary>
    /// Pearson-correlation distance, <c>d(i,j) = sqrt(0.5 * (1 - rho(i,j)))</c>, as in
    /// the original HRP (Lopez de Prado, 2016). The default — preserves historical behavior.
    /// </summary>
    PearsonDistance,

    /// <summary>
    /// Lower-tail-dependence-coefficient (LTDC) distance, <c>d(i,j) = 1 - lambda(i,j)</c>,
    /// where <c>lambda(i,j)</c> is the lower tail dependence coefficient between assets
    /// <c>i</c> and <c>j</c>. This is the actual contribution of Lohre, Rother &amp; Schäfer
    /// (2020), "Hierarchical Risk Parity: Accounting for Tail Dependencies in Multi-asset
    /// Multi-factor Allocations": cluster on co-crash behavior rather than linear correlation,
    /// so assets that tend to crash together are grouped together. Computed via
    /// <c>Boutquin.Numerics.Statistics.LowerTailDependence&lt;double&gt;.EstimateMatrix</c>.
    /// </summary>
    LowerTailDependence,
}
