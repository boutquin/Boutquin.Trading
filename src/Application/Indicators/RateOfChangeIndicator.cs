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

namespace Boutquin.Trading.Application.Indicators;

using Boutquin.Trading.Domain.Exceptions;

public sealed class RateOfChangeIndicator : IMacroIndicator
{
    private readonly int _lookback;

    public RateOfChangeIndicator(int lookback)
    {
        Guard.AgainstNegativeOrZero(() => lookback);
        _lookback = lookback;
    }

    public decimal Compute(decimal[] series1, decimal[] series2)
    {
        Guard.AgainstNullOrEmptyArray(() => series1);
        Guard.AgainstNullOrEmptyArray(() => series2);

        if (series1.Length != series2.Length)
        {
            throw new ArgumentException("Series must have equal length.", nameof(series2));
        }

        if (series1.Length <= _lookback)
        {
            throw new InsufficientDataException(
                $"Need more than {_lookback} data points, got {series1.Length}.");
        }

        var currentSpread = series1[^1] - series2[^1];
        var priorSpread = series1[^(1 + _lookback)] - series2[^(1 + _lookback)];

        if (priorSpread == 0m)
        {
            throw new CalculationException("Prior spread is zero; cannot compute rate of change.");
        }

        return (currentSpread - priorSpread) / Math.Abs(priorSpread);
    }
}
