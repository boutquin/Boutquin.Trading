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

using System.Globalization;
using System.Numerics;

/// <summary>Private generic-N fractional optimizer with exact emitted-vector certification.</summary>
internal static class CertifiedMaximumDiversification
{
    internal const int MaximumProposals = 256;
    private static readonly BigInteger s_units = BigInteger.Pow(10, 28);

    internal static bool TrySolve(decimal[,] covariance, decimal[] volatilities, decimal lower, decimal upper,
        out decimal[] weights, out Diagnostics diagnostics)
    {
        weights = [];
        diagnostics = new Diagnostics();
        var n = volatilities.Length;
        if (covariance.GetLength(0) != n || covariance.GetLength(1) != n ||
            lower < 0m || upper > 1m || lower > upper)
        {
            return false;
        }

        try
        {
            var lo = Rational.From(lower);
            var hi = Rational.From(upper);
            if (lo * n > Rational.One || hi * n < Rational.One)
            {
                return false;
            }

            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (covariance[i, j] != covariance[j, i])
                    {
                        return false;
                    }
                }
            }

            var exact = ExactMatrix(covariance);
            if (!IsPositiveDefinite(exact))
            {
                return false;
            }

            var problem = new Problem(exact, volatilities, lo, hi);
            weights = Solve(covariance, volatilities, lower, upper, problem, diagnostics);
            return true;
        }
        catch (Exception exception) when (exception is ArithmeticException or OutOfMemoryException)
        {
            throw new InvalidOperationException("Maximum diversification numerical arithmetic or resource refusal.", exception);
        }
    }

    private static decimal[] Solve(decimal[,] covariance, decimal[] volatilities, decimal lower, decimal upper,
        Problem problem, Diagnostics diagnostics)
    {
        var n = volatilities.Length;
        var correlation = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                correlation[i, j] = covariance[i, j] / (volatilities[i] * volatilities[j]);
            }
        }

        // Preserve the original fast-path arithmetic; any returned vector is certified anew.
        diagnostics.QpCalls++;
        var normalized = ActiveSetQpSolver<decimal>.SolveMinVariance(correlation, 0m, 1m);
        var fast = new decimal[n];
        var sum = 0m;
        for (var i = 0; i < n; i++)
        {
            fast[i] = normalized[i] / volatilities[i];
            sum += fast[i];
        }

        for (var i = 0; i < n; i++)
        {
            fast[i] /= sum;
        }

        if (problem.Accepts(fast))
        {
            return fast;
        }

        fast = RestoreAndRecord(fast, lower, upper, diagnostics);
        if (problem.Accepts(fast))
        {
            return fast;
        }

        var current = RestoreAndRecord(Enumerable.Repeat(1m / n, n).ToArray(), lower, upper, diagnostics);
        if (problem.Accepts(current))
        {
            return current;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { Key(current) };
        for (var iteration = 0; iteration < MaximumProposals; iteration++)
        {
            var t = problem.RiskAversion(current).ToPositiveDecimal();
            diagnostics.QpCalls++;
            var proposed = ActiveSetQpSolver<decimal>.SolveMeanVariance(covariance, volatilities, t, lower, upper);
            current = RestoreAndRecord(proposed, lower, upper, diagnostics);
            if (problem.Accepts(current))
            {
                return current;
            }

            if (!seen.Add(Key(current)))
            {
                throw new InvalidOperationException("Maximum diversification repeated an uncertified emitted vector.");
            }
        }

        throw new InvalidOperationException("Maximum diversification exhausted 256 proposals without a true-DR certificate.");
    }

    private static string Key(decimal[] weights) => string.Join(";", weights.Select(w => w.ToString("G29", CultureInfo.InvariantCulture)));

    internal sealed class Diagnostics
    {
        internal int QpCalls { get; set; }
        internal List<Rational> RestorationL1Changes { get; } = [];
    }

    private static decimal[] RestoreAndRecord(decimal[] proposal, decimal lower, decimal upper, Diagnostics diagnostics)
    {
        var result = RestoreFeasibility(proposal, lower, upper);
        var change = Rational.Zero;
        for (var i = 0; i < result.Length; i++)
        {
            change += result[i] >= proposal[i] ? Rational.From(result[i]) - Rational.From(proposal[i]) : Rational.From(proposal[i]) - Rational.From(result[i]);
        }

        diagnostics.RestorationL1Changes.Add(change);
        return result;
    }

    internal static decimal[] RestoreFeasibility(decimal[] proposal, decimal lower, decimal upper)
    {
        var lo = (Rational.From(lower) * new Rational(s_units)).Integer;
        var hi = (Rational.From(upper) * new Rational(s_units)).Integer;
        var units = new BigInteger[proposal.Length];
        var residual = s_units;
        for (var i = 0; i < units.Length; i++)
        {
            units[i] = BigInteger.Clamp((Rational.From(proposal[i]) * new Rational(s_units)).Integer, lo, hi);
            residual -= units[i];
        }

        for (var i = 0; i < units.Length && !residual.IsZero; i++)
        {
            var change = residual.Sign > 0 ? BigInteger.Min(residual, hi - units[i]) : -BigInteger.Min(-residual, units[i] - lo);
            units[i] += change;
            residual -= change;
        }

        if (!residual.IsZero)
        {
            throw new InvalidOperationException("Maximum diversification could not restore exact authored feasibility.");
        }

        return units.Select(u => Rational.DecimalFromUnits(u, 28)).ToArray();
    }

    private static Rational[,] ExactMatrix(decimal[,] covariance)
    {
        var n = covariance.GetLength(0);
        var result = new Rational[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                result[i, j] = Rational.From(covariance[i, j]);
            }
        }

        return result;
    }

    private static bool IsPositiveDefinite(Rational[,] covariance)
    {
        var n = covariance.GetLength(0);
        var l = new Rational[n, n];
        var d = new Rational[n];
        for (var i = 0; i < n; i++)
        {
            var diagonal = covariance[i, i];
            for (var k = 0; k < i; k++)
            {
                diagonal -= l[i, k] * l[i, k] * d[k];
            }

            if (diagonal <= Rational.Zero)
            {
                return false;
            }

            d[i] = diagonal;
            for (var j = i + 1; j < n; j++)
            {
                var value = covariance[j, i];
                for (var k = 0; k < i; k++)
                {
                    value -= l[j, k] * l[i, k] * d[k];
                }

                l[j, i] = value / diagonal;
            }
        }

        return true;
    }

    /// <summary>Test-visible certificate; production constructs the same problem only after exact SPD admission.</summary>
    internal static bool HasCertificate(decimal[,] covariance, decimal[] volatilities, decimal[] weights,
        decimal lower, decimal upper) => new Problem(ExactMatrix(covariance), volatilities, Rational.From(lower), Rational.From(upper)).Accepts(weights);

    private sealed class Problem
    {
        private readonly Rational[,] _covariance;
        private readonly Rational[] _volatilities;
        private readonly Rational _lower;
        private readonly Rational _upper;
        private readonly Rational _minimumVolatility;
        private readonly Rational _alpha;
        private readonly Rational _beta;

        internal Problem(Rational[,] covariance, decimal[] volatilities, Rational lower, Rational upper)
        {
            _covariance = covariance;
            _volatilities = volatilities.Select(Rational.From).ToArray();
            _lower = lower;
            _upper = upper;
            var scale = BigInteger.Pow(10, 96);
            for (var i = 0; i < volatilities.Length; i++)
            {
                if (_volatilities[i] <= Rational.Zero)
                {
                    throw new InvalidOperationException("Maximum diversification requires a positive volatility approximation.");
                }

                var variance = covariance[i, i];
                var root = IntegerSquareRoot(variance.Numerator * scale * scale / variance.Denominator);
                var lo = new Rational(root, scale);
                var hi = new Rational(root + 1, scale);
                if (lo * lo > variance || hi * hi <= variance)
                {
                    throw new InvalidOperationException("Maximum diversification volatility enclosure failed.");
                }

                var a = lo / _volatilities[i];
                var b = hi / _volatilities[i];
                _alpha = i == 0 || a < _alpha ? a : _alpha;
                _beta = i == 0 || b > _beta ? b : _beta;
            }

            _minimumVolatility = MinimumLinear(_volatilities);
            if (_alpha <= Rational.Zero || _minimumVolatility <= Rational.Zero)
            {
                throw new InvalidOperationException("Maximum diversification could not certify positive volatility.");
            }
        }

        internal Rational RiskAversion(decimal[] weights)
        {
            var w = weights.Select(Rational.From).ToArray();
            return Dot(_volatilities, w) / Dot(w, Multiply(w));
        }

        internal bool Accepts(decimal[] weights)
        {
            if (weights.Length != _volatilities.Length)
            {
                return false;
            }

            var w = weights.Select(Rational.From).ToArray();
            var sum = Rational.Zero;
            foreach (var value in w)
            {
                if (value < _lower || value > _upper)
                {
                    return false;
                }

                sum += value;
            }

            if (sum != Rational.One)
            {
                return false;
            }

            var v = Dot(_volatilities, w);
            var cw = Multiply(w);
            var f = Dot(w, cw) / (v * v);
            var coefficients = new Rational[w.Length];
            for (var i = 0; i < w.Length; i++)
            {
                coefficients[i] = 2 * cw[i] / v - 2 * f * _volatilities[i];
            }

            var d = MinimumLinear(coefficients);
            var bound = f + (d < Rational.Zero ? d : Rational.Zero) / _minimumVolatility;
            var accuracyFactor = new Rational(10000000001, 10000000000);
            return bound > Rational.Zero &&
                _beta * _beta * f <= _alpha * _alpha * accuracyFactor * accuracyFactor * bound;
        }

        private Rational[] Multiply(Rational[] w)
        {
            var result = new Rational[w.Length];
            for (var i = 0; i < w.Length; i++)
            {
                for (var j = 0; j < w.Length; j++)
                {
                    result[i] += _covariance[i, j] * w[j];
                }
            }

            return result;
        }

        private Rational MinimumLinear(Rational[] coefficients)
        {
            var indices = Enumerable.Range(0, coefficients.Length).OrderBy(i => coefficients[i]).ThenBy(i => i);
            var remaining = Rational.One - _lower * coefficients.Length;
            var result = Rational.Zero;
            foreach (var coefficient in coefficients)
            {
                result += coefficient * _lower;
            }

            foreach (var i in indices)
            {
                var capacity = _upper - _lower;
                var amount = remaining < capacity ? remaining : capacity;
                result += amount * coefficients[i];
                remaining -= amount;
            }

            if (remaining != Rational.Zero)
            {
                throw new InvalidOperationException("Maximum diversification certificate LP is infeasible.");
            }

            return result;
        }
    }

    private static Rational Dot(Rational[] left, Rational[] right)
    {
        var sum = Rational.Zero;
        for (var i = 0; i < left.Length; i++)
        {
            sum += left[i] * right[i];
        }

        return sum;
    }

    internal static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new InvalidOperationException("Maximum diversification cannot enclose a negative variance.");
        }

        if (value < 2)
        {
            return value;
        }

        var bits = checked((int)value.GetBitLength());
        var x = BigInteger.One << ((bits + 1) / 2);
        for (var iteration = 0; iteration <= bits; iteration++)
        {
            var next = (x + value / x) >> 1;
            if (next >= x)
            {
                return x;
            }

            x = next;
        }

        throw new InvalidOperationException("Maximum diversification integer square root exhausted its bit-length bound.");
    }

    internal readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
    {
        private readonly BigInteger _denominator;
        internal BigInteger Numerator { get; }
        internal BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        internal BigInteger Integer => Denominator.IsOne ? Numerator : throw new InvalidOperationException("Expected exact lattice integer.");
        internal static Rational Zero => default;
        internal static Rational One => new(1);

        internal Rational(BigInteger numerator) : this(numerator, BigInteger.One) { }

        internal Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new DivideByZeroException();
            }

            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator * denominator.Sign / gcd;
            _denominator = BigInteger.Abs(denominator) / gcd;
        }

        internal static Rational From(decimal value)
        {
            var bits = decimal.GetBits(value);
            var coefficient = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
            return new Rational(bits[3] < 0 ? -coefficient : coefficient, BigInteger.Pow(10, (bits[3] >> 16) & 0xff));
        }

        internal decimal ToPositiveDecimal()
        {
            var maximum = (BigInteger.One << 96) - 1;
            if (this <= Zero || this > new Rational(maximum))
            {
                throw new InvalidOperationException("Maximum diversification risk aversion is not positive and representable.");
            }

            var best = Zero;
            var distance = this;
            var bestUnits = BigInteger.Zero;
            var bestScale = 0;
            for (var scale = 0; scale <= 28; scale++)
            {
                var power = BigInteger.Pow(10, scale);
                var whole = BigInteger.DivRem(Numerator * power, Denominator, out var remainder);
                if (2 * remainder > Denominator || (2 * remainder == Denominator && !whole.IsEven))
                {
                    whole++;
                }

                whole = BigInteger.Min(whole, maximum);
                var candidate = new Rational(whole, power);
                var delta = candidate > this ? candidate - this : this - candidate;
                var commonScale = Math.Max(scale, bestScale);
                var candidateEven = (whole * BigInteger.Pow(10, commonScale - scale)).IsEven;
                var bestEven = (bestUnits * BigInteger.Pow(10, commonScale - bestScale)).IsEven;
                if (delta < distance || (delta == distance && candidateEven && !bestEven))
                {
                    best = candidate;
                    distance = delta;
                    bestUnits = whole;
                    bestScale = scale;
                }
            }

            if (best == Zero)
            {
                throw new InvalidOperationException("Maximum diversification risk aversion rounded to zero.");
            }

            return DecimalFromUnits(bestUnits, (byte)bestScale);
        }

        internal static decimal DecimalFromUnits(BigInteger units, byte scale) => new(
            unchecked((int)(uint)(units & uint.MaxValue)),
            unchecked((int)(uint)((units >> 32) & uint.MaxValue)),
            unchecked((int)(uint)((units >> 64) & uint.MaxValue)), false, scale);

        public int CompareTo(Rational other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
        public override bool Equals(object? obj) => obj is Rational other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
        public static Rational operator +(Rational a, Rational b) => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static Rational operator -(Rational a, Rational b) => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
        public static Rational operator *(Rational a, Rational b) => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        public static Rational operator /(Rational a, Rational b) => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        public static implicit operator Rational(int value) => new(value);
        public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;
        public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;
        public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;
        public static bool operator ==(Rational a, Rational b) => a.Equals(b);
        public static bool operator !=(Rational a, Rational b) => !a.Equals(b);
    }
}
