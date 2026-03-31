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

using System.Text.Json;

namespace Boutquin.Trading.Tests.UnitTests;

/// <summary>
/// Base class for cross-language verification tests.
/// Provides shared utilities for loading Python-generated JSON vectors
/// and asserting results within precision tiers.
/// </summary>
public abstract class CrossLanguageVerificationBase
{
    // ─── Precision tiers (match Python conftest.py) ─────────────────────
    /// <summary>Exact arithmetic: returns, simple ratios.</summary>
    protected const decimal PrecisionExact = 1e-10m;

    /// <summary>Iterative / floating-point heavy: optimizers, matrix ops.</summary>
    protected const decimal PrecisionNumeric = 1e-6m;

    /// <summary>Statistical sampling: Monte Carlo.</summary>
    protected const decimal PrecisionStatistical = 1e-4m;

    protected const int TradingDaysPerYear = 252;

    // ─── Vector loading ─────────────────────────────────────────────────

    private static string? s_vectorsDir;

    /// <summary>
    /// Resolves the vectors directory. Walks up from the test assembly location
    /// until it finds tests/Verification/vectors/.
    /// </summary>
    protected static string GetVectorsDir()
    {
        if (s_vectorsDir != null)
        {
            return s_vectorsDir;
        }

        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tests", "Verification", "vectors");
            if (Directory.Exists(candidate))
            {
                s_vectorsDir = candidate;
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Cannot find tests/Verification/vectors/ directory. " +
            "Run the Python vector generators in tests/Verification/ first.");
    }

    protected static JsonDocument LoadVector(string name)
    {
        var path = Path.Combine(GetVectorsDir(), $"{name}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Vector file not found: {path}. Run the Python generator first.",
                path);
        }

        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    // ─── JSON element helpers ───────────────────────────────────────────

    protected static decimal[] GetDecimalArray(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(e => (decimal)e.GetDouble())
            .ToArray();
    }

    protected static decimal GetDecimal(JsonElement parent, string propertyName)
    {
        return (decimal)parent.GetProperty(propertyName).GetDouble();
    }

    /// <summary>
    /// Checks if a JSON property value indicates that the C# code should throw.
    /// Convention: "EXCEPTION:ExceptionTypeName" in the JSON.
    /// </summary>
    protected static bool IsExpectedException(JsonElement parent, string propertyName, out string exceptionType)
    {
        var prop = parent.GetProperty(propertyName);
        if (prop.ValueKind == JsonValueKind.String)
        {
            var val = prop.GetString()!;
            if (val.StartsWith("EXCEPTION:", StringComparison.Ordinal))
            {
                exceptionType = val["EXCEPTION:".Length..];
                return true;
            }
        }

        exceptionType = "";
        return false;
    }

    // ─── Assertion helpers ──────────────────────────────────────────────

    protected static void AssertWithinTolerance(decimal actual, decimal expected, decimal tolerance, string label = "")
    {
        var diff = Math.Abs(actual - expected);
        Assert.True(diff <= tolerance,
            $"{label}Expected {expected}, got {actual}, diff={diff} > tolerance={tolerance}");
    }
}
