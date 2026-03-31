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

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Result of a single walk-forward fold: in-sample parameter selection and out-of-sample evaluation.
/// </summary>
/// <param name="FoldIndex">Zero-based fold index.</param>
/// <param name="InSampleStart">Start date of in-sample period.</param>
/// <param name="InSampleEnd">End date of in-sample period.</param>
/// <param name="OutOfSampleStart">Start date of out-of-sample period.</param>
/// <param name="OutOfSampleEnd">End date of out-of-sample period.</param>
/// <param name="SelectedParameterIndex">Index of the parameter set selected in-sample.</param>
/// <param name="InSampleSharpe">Sharpe ratio achieved in-sample with the selected parameter.</param>
/// <param name="OutOfSampleSharpe">Sharpe ratio achieved out-of-sample with the selected parameter.</param>
public sealed record WalkForwardResult(
    int FoldIndex,
    DateOnly InSampleStart,
    DateOnly InSampleEnd,
    DateOnly OutOfSampleStart,
    DateOnly OutOfSampleEnd,
    int SelectedParameterIndex,
    decimal InSampleSharpe,
    decimal OutOfSampleSharpe);
