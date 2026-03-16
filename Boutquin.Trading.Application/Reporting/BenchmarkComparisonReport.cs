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

using System.Globalization;
using System.Text;

using Boutquin.Domain.Extensions;

namespace Boutquin.Trading.Application.Reporting;

/// <summary>
/// Generates an HTML benchmark comparison report showing side-by-side metrics,
/// relative equity curves, tracking error, and information ratio over time.
/// </summary>
public static class BenchmarkComparisonReport
{
    /// <summary>
    /// Generates a side-by-side comparison HTML report for portfolio vs benchmark.
    /// </summary>
    /// <param name="portfolio">Portfolio tearsheet.</param>
    /// <param name="benchmark">Benchmark tearsheet.</param>
    /// <param name="portfolioName">Display name for the portfolio.</param>
    /// <param name="benchmarkName">Display name for the benchmark.</param>
    /// <returns>A string containing the complete HTML document.</returns>
    public static string Generate(
        Tearsheet portfolio,
        Tearsheet benchmark,
        string portfolioName,
        string benchmarkName)
    {
        // Compute tracking error from equity curves
        var portfolioDailyReturns = ExtractDailyReturns(portfolio.EquityCurve);
        var benchmarkDailyReturns = ExtractDailyReturns(benchmark.EquityCurve);
        var trackingError = portfolioDailyReturns.Length > 0 && benchmarkDailyReturns.Length > 0
            && portfolioDailyReturns.Length == benchmarkDailyReturns.Length
            ? CalculateTrackingError(portfolioDailyReturns, benchmarkDailyReturns)
            : 0m;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{Encode(portfolioName)} vs {Encode(benchmarkName)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <h1>{Encode(portfolioName)} vs {Encode(benchmarkName)}</h1>");

        // Side-by-side metrics table
        sb.AppendLine("  <h2>Performance Comparison</h2>");
        sb.AppendLine("  <table class=\"comparison\">");
        sb.AppendLine("    <tr><th>Metric</th><th>Portfolio</th><th>Benchmark</th></tr>");
        AppendCompRow(sb, "Annualized Return", portfolio.AnnualizedReturn, benchmark.AnnualizedReturn, "P2");
        AppendCompRow(sb, "CAGR", portfolio.CAGR, benchmark.CAGR, "P2");
        AppendCompRow(sb, "Volatility", portfolio.Volatility, benchmark.Volatility, "P2");
        AppendCompRow(sb, "Sharpe Ratio", portfolio.SharpeRatio, benchmark.SharpeRatio, "F2");
        AppendCompRow(sb, "Sortino Ratio", portfolio.SortinoRatio, benchmark.SortinoRatio, "F2");
        AppendCompRow(sb, "Max Drawdown", portfolio.MaxDrawdown, benchmark.MaxDrawdown, "P2");
        AppendCompRow(sb, "Calmar Ratio", portfolio.CalmarRatio, benchmark.CalmarRatio, "F2");
        AppendCompRow(sb, "Alpha", portfolio.Alpha, benchmark.Alpha, "F4");
        AppendCompRow(sb, "Beta", portfolio.Beta, benchmark.Beta, "F4");
        AppendCompRow(sb, "Information Ratio", portfolio.InformationRatio, benchmark.InformationRatio, "F2");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    <tr><td><strong>Tracking Error</strong></td><td colspan=\"2\" style=\"text-align:center\">{trackingError.ToString("P2", CultureInfo.InvariantCulture)}</td></tr>");
        sb.AppendLine("  </table>");

        // Equity curve comparison
        sb.AppendLine("  <h2>Equity Curves</h2>");
        sb.AppendLine("  <div class=\"chart\">");
        sb.AppendLine(GenerateDualEquityCurveSvg(
            portfolio.EquityCurve, benchmark.EquityCurve,
            portfolioName, benchmarkName));
        sb.AppendLine("  </div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Calculates the annualized tracking error between portfolio and benchmark returns.
    /// </summary>
    /// <param name="portfolioReturns">Portfolio daily returns.</param>
    /// <param name="benchmarkReturns">Benchmark daily returns.</param>
    /// <returns>Annualized tracking error (standard deviation of active returns × √252).</returns>
    public static decimal CalculateTrackingError(decimal[] portfolioReturns, decimal[] benchmarkReturns)
    {
        if (portfolioReturns.Length != benchmarkReturns.Length)
        {
            throw new ArgumentException(
                "Portfolio and benchmark return arrays must have the same length.",
                nameof(benchmarkReturns));
        }

        var activeReturns = portfolioReturns
            .Zip(benchmarkReturns, (p, b) => p - b)
            .ToArray();

        return activeReturns.StandardDeviation() * (decimal)Math.Sqrt(252);
    }

    private static decimal[] ExtractDailyReturns(SortedDictionary<DateOnly, decimal> equityCurve)
    {
        var values = equityCurve.Values.ToArray();
        if (values.Length < 2)
        {
            return [];
        }

        var returns = new decimal[values.Length - 1];
        for (var i = 1; i < values.Length; i++)
        {
            returns[i - 1] = values[i - 1] != 0 ? (values[i] / values[i - 1]) - 1 : 0m;
        }

        return returns;
    }

    private static string GetCss()
    {
        return """
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 1000px; margin: 0 auto; padding: 20px; background: #fafafa; }
            h1 { color: #1a1a2e; border-bottom: 2px solid #16213e; padding-bottom: 10px; }
            h2 { color: #16213e; margin-top: 30px; }
            table.comparison { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
            table.comparison th { background: #16213e; color: white; padding: 10px 12px; text-align: center; }
            table.comparison th:first-child { text-align: left; }
            table.comparison td { padding: 8px 12px; border-bottom: 1px solid #e0e0e0; }
            table.comparison td:first-child { font-weight: 600; color: #333; }
            table.comparison td:not(:first-child) { text-align: center; font-family: 'SF Mono', 'Fira Code', monospace; }
            .chart { margin: 15px 0; overflow-x: auto; }
            svg { max-width: 100%; height: auto; }
            .legend { font-size: 12px; }
            """;
    }

    private static void AppendCompRow(StringBuilder sb, string label, decimal portfolioVal, decimal benchmarkVal, string format)
    {
        var pFormatted = FormatValue(portfolioVal, format);
        var bFormatted = FormatValue(benchmarkVal, format);
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"    <tr><td>{Encode(label)}</td><td>{pFormatted}</td><td>{bFormatted}</td></tr>");
    }

    private static string FormatValue(decimal value, string format) => format switch
    {
        "P2" => value.ToString("P2", CultureInfo.InvariantCulture),
        "F2" => value.ToString("F2", CultureInfo.InvariantCulture),
        "F4" => value.ToString("F4", CultureInfo.InvariantCulture),
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string GenerateDualEquityCurveSvg(
        SortedDictionary<DateOnly, decimal> portfolioCurve,
        SortedDictionary<DateOnly, decimal> benchmarkCurve,
        string portfolioName,
        string benchmarkName)
    {
        if (portfolioCurve.Count < 2 || benchmarkCurve.Count < 2)
        {
            return "<p>Insufficient data for equity curve comparison.</p>";
        }

        const int width = 960;
        const int height = 300;
        const int margin = 40;

        // Normalize both curves to start at 100
        var pEntries = portfolioCurve.ToList();
        var bEntries = benchmarkCurve.ToList();
        var pBase = pEntries[0].Value;
        var bBase = bEntries[0].Value;

        var allNormalized = pEntries.Select(e => pBase != 0 ? e.Value / pBase * 100 : 100m)
            .Concat(bEntries.Select(e => bBase != 0 ? e.Value / bBase * 100 : 100m))
            .ToList();

        var minVal = allNormalized.Min();
        var maxVal = allNormalized.Max();
        var range = maxVal - minVal;
        if (range == 0)
        {
            range = 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {width} {height + 30}\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Portfolio line
        sb.Append("<polyline fill=\"none\" stroke=\"#0d47a1\" stroke-width=\"2\" points=\"");
        for (var i = 0; i < pEntries.Count; i++)
        {
            var normalized = pBase != 0 ? pEntries[i].Value / pBase * 100 : 100m;
            var x = margin + (decimal)i / (pEntries.Count - 1) * (width - 2 * margin);
            var y = height - margin - (normalized - minVal) / range * (height - 2 * margin);
            sb.Append(CultureInfo.InvariantCulture, $"{x:F1},{y:F1} ");
        }

        sb.AppendLine("\" />");

        // Benchmark line
        sb.Append("<polyline fill=\"none\" stroke=\"#e65100\" stroke-width=\"2\" stroke-dasharray=\"5,3\" points=\"");
        for (var i = 0; i < bEntries.Count; i++)
        {
            var normalized = bBase != 0 ? bEntries[i].Value / bBase * 100 : 100m;
            var x = margin + (decimal)i / (bEntries.Count - 1) * (width - 2 * margin);
            var y = height - margin - (normalized - minVal) / range * (height - 2 * margin);
            sb.Append(CultureInfo.InvariantCulture, $"{x:F1},{y:F1} ");
        }

        sb.AppendLine("\" />");

        // Legend
        var legendY = height + 10;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<line x1=\"{margin}\" y1=\"{legendY}\" x2=\"{margin + 30}\" y2=\"{legendY}\" stroke=\"#0d47a1\" stroke-width=\"2\" />");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<text x=\"{margin + 35}\" y=\"{legendY + 4}\" class=\"legend\">{Encode(portfolioName)}</text>");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<line x1=\"{margin + 200}\" y1=\"{legendY}\" x2=\"{margin + 230}\" y2=\"{legendY}\" stroke=\"#e65100\" stroke-width=\"2\" stroke-dasharray=\"5,3\" />");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"<text x=\"{margin + 235}\" y=\"{legendY + 4}\" class=\"legend\">{Encode(benchmarkName)}</text>");

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string Encode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);
}
