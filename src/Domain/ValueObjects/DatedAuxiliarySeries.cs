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

namespace Boutquin.Trading.Domain.ValueObjects;

/// <summary>
/// A date-indexed collection of auxiliary (non-price) series — yields, curve points, income yields —
/// that can be sliced to the observations known <b>at</b> a given evaluation date, so that a consumer
/// evaluating many historical rebalances from one bound instance never reads observations dated after
/// the decision point (lookahead bias).
/// </summary>
/// <remarks>
/// <para><b>Role:</b> the date-aware carrier for the <c>auxiliarySeries</c> channel consumed by the
/// cross-sectional signal layer. The positional form of that channel
/// (<c>IReadOnlyDictionary&lt;Symbol, decimal[]&gt;</c>) carries no dates: signals read it from the
/// tail (a trailing-window cumulative return, a most-recent observation), which is correct only when
/// the caller guarantees the array ends at the decision date. A consumer that binds one positional
/// array for a whole backtest therefore feeds every historical rebalance the run-end tail — lookahead.
/// This type removes that failure mode structurally: bind the full date-indexed history once, and
/// <see cref="SliceUpTo"/> produces, per rebalance, exactly the positional prefix a signal may know at
/// that date.</para>
/// <para><b>Ordering invariant:</b> every series must be strictly ascending by date (no duplicate
/// dates). The constructor validates this eagerly and names the offending key, so a mis-ordered fetch
/// surfaces at bind time rather than as a silently wrong slice at some later rebalance.</para>
/// <para><b>Immutability and thread-safety:</b> the constructor takes a defensive copy of every
/// series; later mutation of the caller's collections does not affect this instance. Instances are
/// immutable after construction and safe for concurrent <see cref="SliceUpTo"/> calls.</para>
/// <para><b>Key space:</b> keys follow the same convention as the positional channel — per-asset
/// symbols (for example an ETF's trailing income yield) and well-known market-wide series identifiers
/// (for example the FRED <c>DGS10</c>/<c>DGS2</c>/<c>DGS3MO</c> identifiers). This type does not
/// interpret keys; each signal documents which keys it reads.</para>
/// </remarks>
public sealed class DatedAuxiliarySeries
{
    private readonly Dictionary<Symbol, (DateOnly Date, decimal Value)[]> _series;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatedAuxiliarySeries"/> class from date-indexed
    /// observations, validating that every series is strictly ascending by date.
    /// </summary>
    /// <param name="series">
    /// The date-indexed series keyed by <see cref="Symbol"/> (per-asset and/or market-wide keys). Each
    /// value is that key's observation list in strictly ascending date order. An empty dictionary is
    /// valid (every slice is empty); an empty observation list is valid (that key never appears in a
    /// slice). Validated non-null; the observations are defensively copied.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="series"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown, naming the offending key, when any observation list is <see langword="null"/> or is not
    /// strictly ascending by date (out-of-order or duplicate dates).
    /// </exception>
    public DatedAuxiliarySeries(
        IReadOnlyDictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>> series)
    {
        Guard.AgainstNull(() => series);

        _series = new Dictionary<Symbol, (DateOnly Date, decimal Value)[]>(series.Count);
        foreach (var (key, observations) in series)
        {
            if (observations is null)
            {
                throw new ArgumentException(
                    $"The observation list for auxiliary series '{key}' is null.",
                    nameof(series));
            }

            var copy = new (DateOnly Date, decimal Value)[observations.Count];
            for (var i = 0; i < observations.Count; i++)
            {
                copy[i] = observations[i];
                if (i > 0 && copy[i].Date <= copy[i - 1].Date)
                {
                    throw new ArgumentException(
                        $"The observation list for auxiliary series '{key}' is not strictly ascending " +
                        $"by date: observation {i} ({copy[i].Date:yyyy-MM-dd}) does not follow " +
                        $"observation {i - 1} ({copy[i - 1].Date:yyyy-MM-dd}).",
                        nameof(series));
                }
            }

            _series[key] = copy;
        }
    }

    /// <summary>
    /// Creates a <see cref="DatedAuxiliarySeries"/> directly from economic-series maps in the
    /// <c>SortedDictionary&lt;DateOnly, decimal&gt;</c> shape (the form backtest datasets deliver
    /// economic data in), so composition needs no hand-flattening of dates into positional arrays.
    /// </summary>
    /// <param name="economicSeries">
    /// The date-keyed series per <see cref="Symbol"/>. A <see cref="SortedDictionary{TKey, TValue}"/>
    /// enumerates its unique keys in ascending order, so the strict-ascension invariant holds by
    /// construction. Validated non-null; a <see langword="null"/> series value is rejected naming the
    /// key. The observations are copied — later mutation of the caller's dictionaries does not affect
    /// the returned instance.
    /// </param>
    /// <returns>An immutable <see cref="DatedAuxiliarySeries"/> over the same observations.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="economicSeries"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown, naming the offending key, when a series value is <see langword="null"/>.
    /// </exception>
    public static DatedAuxiliarySeries FromEconomicSeries(
        IReadOnlyDictionary<Symbol, SortedDictionary<DateOnly, decimal>> economicSeries)
    {
        Guard.AgainstNull(() => economicSeries);

        var series = new Dictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>(economicSeries.Count);
        foreach (var (key, observations) in economicSeries)
        {
            if (observations is null)
            {
                throw new ArgumentException(
                    $"The economic series for auxiliary key '{key}' is null.",
                    nameof(economicSeries));
            }

            var copy = new (DateOnly Date, decimal Value)[observations.Count];
            var i = 0;
            foreach (var (date, value) in observations)
            {
                copy[i++] = (date, value);
            }

            series[key] = copy;
        }

        return new DatedAuxiliarySeries(series);
    }

    /// <summary>
    /// Produces the positional auxiliary-series view known at <paramref name="asOf"/>: for each key,
    /// the values of every observation dated on or before <paramref name="asOf"/>, in date order.
    /// </summary>
    /// <param name="asOf">
    /// The evaluation (decision) date, inclusive — an observation dated exactly
    /// <paramref name="asOf"/> is part of the slice.
    /// </param>
    /// <returns>
    /// A dictionary in the positional <c>auxiliarySeries</c> shape consumed by the signal layer. Each
    /// array's last element is the key's most recent observation at <paramref name="asOf"/>, so a
    /// signal's tail-read (a most-recent value, a trailing-window cumulative return) is correctly
    /// dated by construction. A key with no observation on or before <paramref name="asOf"/> is
    /// <b>omitted</b> (absent key, matching the signal layer's absent-key contract), never given an
    /// empty array. The result is never <see langword="null"/> and is a fresh mutable-safe snapshot on
    /// every call.
    /// </returns>
    /// <remarks>
    /// <para><b>Complexity:</b> O(log n) binary search plus an O(k) copy per key, where n is the key's
    /// observation count and k the number of observations in the slice.</para>
    /// </remarks>
    public IReadOnlyDictionary<Symbol, decimal[]> SliceUpTo(DateOnly asOf)
    {
        var sliced = new Dictionary<Symbol, decimal[]>(_series.Count);
        foreach (var (key, observations) in _series)
        {
            var count = CountOnOrBefore(observations, asOf);
            if (count == 0)
            {
                continue;
            }

            var values = new decimal[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = observations[i].Value;
            }

            sliced[key] = values;
        }

        return sliced;
    }

    /// <summary>
    /// Counts the observations dated on or before <paramref name="asOf"/> — the length of the slice
    /// prefix — via binary search over the strictly ascending dates.
    /// </summary>
    private static int CountOnOrBefore((DateOnly Date, decimal Value)[] observations, DateOnly asOf)
    {
        var lo = 0;
        var hi = observations.Length; // first index with Date > asOf
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (observations[mid].Date <= asOf)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
