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

public sealed class ExponentialMovingAverage : IIndicator
{
    private readonly int _period;

    public ExponentialMovingAverage(int period)
    {
        Guard.AgainstNegativeOrZero(() => period);
        _period = period;
    }

    public decimal Compute(decimal[] values)
    {
        Guard.AgainstNullOrEmptyArray(() => values);
        if (values.Length < _period)
        {
            throw new InsufficientDataException(
                $"Need at least {_period} values to compute EMA({_period}), got {values.Length}.");
        }

        var multiplier = 2m / (_period + 1);
        // Seed with SMA of first _period values (allocation-free)
        var seed = 0m;
        for (var i = 0; i < _period; i++)
        {
            seed += values[i];
        }

        var ema = seed / _period;

        for (var i = _period; i < values.Length; i++)
        {
            ema = (values[i] - ema) * multiplier + ema;
        }

        return ema;
    }
}
