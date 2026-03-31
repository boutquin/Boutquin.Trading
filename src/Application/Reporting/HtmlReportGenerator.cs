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

namespace Boutquin.Trading.Application.Reporting;

/// <summary>
/// Generates an interactive HTML tearsheet report with embedded charts (SVG-based)
/// and a performance metrics summary table.
/// </summary>
public static class HtmlReportGenerator
{
    /// <summary>
    /// Generates a complete HTML tearsheet report for a strategy.
    /// </summary>
    /// <param name="tearsheet">The tearsheet with all performance metrics.</param>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <returns>A string containing the complete HTML document.</returns>
    public static string GenerateTearsheetReport(Tearsheet tearsheet, string strategyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{Encode(strategyName)} — Tearsheet</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <h1>{Encode(strategyName)}</h1>");

        // Metrics table
        sb.AppendLine("  <h2>Performance Metrics</h2>");
        sb.AppendLine("  <table class=\"metrics\">");
        AppendMetricRow(sb, "CAGR", tearsheet.CAGR, "P2");
        AppendMetricRow(sb, "Volatility", tearsheet.Volatility, "P2");
        AppendMetricRow(sb, "Sharpe Ratio", tearsheet.SharpeRatio, "F2");
        AppendMetricRow(sb, "Sortino Ratio", tearsheet.SortinoRatio, "F2");
        AppendMetricRow(sb, "Calmar Ratio", tearsheet.CalmarRatio, "F2");
        AppendMetricRow(sb, "Max Drawdown", tearsheet.MaxDrawdown, "P2");
        AppendMetricRow(sb, "Max Drawdown Duration", tearsheet.MaxDrawdownDuration, "days");
        AppendMetricRow(sb, "Alpha", tearsheet.Alpha, "F4");
        AppendMetricRow(sb, "Beta", tearsheet.Beta, "F4");
        AppendMetricRow(sb, "Information Ratio", tearsheet.InformationRatio, "F2");
        AppendMetricRow(sb, "Omega Ratio", tearsheet.OmegaRatio, "F2");
        AppendMetricRow(sb, "Historical VaR (95%)", tearsheet.HistoricalVaR, "P2");
        AppendMetricRow(sb, "Conditional VaR (95%)", tearsheet.ConditionalVaR, "P2");
        AppendMetricRow(sb, "Skewness", tearsheet.Skewness, "F4");
        AppendMetricRow(sb, "Kurtosis", tearsheet.Kurtosis, "F4");
        AppendMetricRow(sb, "Win Rate", tearsheet.WinRate, "P2");
        AppendMetricRow(sb, "Profit Factor", tearsheet.ProfitFactor, "F2");
        AppendMetricRow(sb, "Recovery Factor", tearsheet.RecoveryFactor, "F2");
        sb.AppendLine("  </table>");

        // Equity Curve section
        sb.AppendLine("  <h2>Equity Curve</h2>");
        sb.AppendLine("  <div class=\"chart\">");
        sb.AppendLine(GenerateEquityCurveSvg(tearsheet.EquityCurve));
        sb.AppendLine("  </div>");

        // Drawdown section
        sb.AppendLine("  <h2>Drawdown</h2>");
        sb.AppendLine("  <div class=\"chart\">");
        sb.AppendLine(GenerateDrawdownSvg(tearsheet.Drawdowns));
        sb.AppendLine("  </div>");

        // Monthly Returns section
        sb.AppendLine("  <h2>Monthly Returns</h2>");
        sb.AppendLine(GenerateMonthlyReturnsTable(tearsheet.EquityCurve));

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GetCss()
    {
        return """
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 1000px; margin: 0 auto; padding: 20px; background: #fafafa; }
            h1 { color: #1a1a2e; border-bottom: 2px solid #16213e; padding-bottom: 10px; }
            h2 { color: #16213e; margin-top: 30px; }
            table.metrics { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
            table.metrics td { padding: 8px 12px; border-bottom: 1px solid #e0e0e0; }
            table.metrics td:first-child { font-weight: 600; width: 40%; color: #333; }
            table.metrics td:last-child { text-align: right; font-family: 'SF Mono', 'Fira Code', monospace; }
            .chart { margin: 15px 0; overflow-x: auto; }
            svg { max-width: 100%; height: auto; }
            table.heatmap { border-collapse: collapse; font-size: 12px; }
            table.heatmap th, table.heatmap td { padding: 4px 8px; text-align: center; border: 1px solid #ccc; }
            table.heatmap th { background: #16213e; color: white; }
            .positive { background-color: #c8e6c9; }
            .negative { background-color: #ffcdd2; }
            """;
    }

    private static void AppendMetricRow(StringBuilder sb, string label, decimal value, string format)
    {
        var formatted = format switch
        {
            "P2" => value.ToString("P2", CultureInfo.InvariantCulture),
            "F2" => value.ToString("F2", CultureInfo.InvariantCulture),
            "F4" => value.ToString("F4", CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture)
        };

        sb.AppendLine(CultureInfo.InvariantCulture, $"    <tr><td>{Encode(label)}</td><td>{formatted}</td></tr>");
    }

    private static void AppendMetricRow(StringBuilder sb, string label, int value, string unit)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"    <tr><td>{Encode(label)}</td><td>{value} {unit}</td></tr>");
    }

    private static string GenerateEquityCurveSvg(SortedDictionary<DateOnly, decimal> equityCurve)
    {
        if (equityCurve.Count < 2)
        {
            return "<p>Insufficient data for equity curve chart.</p>";
        }

        const int width = 960;
        const int height = 300;
        const int margin = 40;

        var entries = equityCurve.ToList();
        var minVal = entries.Min(e => e.Value);
        var maxVal = entries.Max(e => e.Value);
        var range = maxVal - minVal;
        if (range == 0)
        {
            range = 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append("<polyline fill=\"none\" stroke=\"#0d47a1\" stroke-width=\"2\" points=\"");

        for (var i = 0; i < entries.Count; i++)
        {
            var x = margin + (decimal)i / (entries.Count - 1) * (width - 2 * margin);
            var y = height - margin - (entries[i].Value - minVal) / range * (height - 2 * margin);
            sb.Append(CultureInfo.InvariantCulture, $"{x:F1},{y:F1} ");
        }

        sb.AppendLine("\" />");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static string GenerateDrawdownSvg(SortedDictionary<DateOnly, decimal> drawdowns)
    {
        if (drawdowns.Count < 2)
        {
            return "<p>Insufficient data for drawdown chart.</p>";
        }

        const int width = 960;
        const int height = 200;
        const int margin = 40;

        var entries = drawdowns.ToList();
        var minDd = entries.Min(e => e.Value);
        if (minDd >= 0)
        {
            minDd = -0.01m; // Ensure visible scale
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<svg viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Zero line
        sb.AppendLine(CultureInfo.InvariantCulture, $"<line x1=\"{margin}\" y1=\"{margin}\" x2=\"{width - margin}\" y2=\"{margin}\" stroke=\"#999\" stroke-width=\"1\" />");

        // Fill area
        sb.Append("<polygon fill=\"rgba(211,47,47,0.3)\" stroke=\"#d32f2f\" stroke-width=\"1\" points=\"");
        sb.Append(CultureInfo.InvariantCulture, $"{margin},{margin} ");

        for (var i = 0; i < entries.Count; i++)
        {
            var x = margin + (decimal)i / (entries.Count - 1) * (width - 2 * margin);
            var y = margin + entries[i].Value / minDd * (height - 2 * margin);
            sb.Append(CultureInfo.InvariantCulture, $"{x:F1},{y:F1} ");
        }

        sb.Append(CultureInfo.InvariantCulture, $"{width - margin},{margin} ");
        sb.AppendLine("\" />");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static string GenerateMonthlyReturnsTable(SortedDictionary<DateOnly, decimal> equityCurve)
    {
        var monthlyReturns = equityCurve.MonthlyReturns();
        if (monthlyReturns.Count == 0)
        {
            return "<p>Insufficient data for monthly returns.</p>";
        }

        var years = monthlyReturns.Keys.Select(k => k.Year).Distinct().OrderBy(y => y).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<table class=\"heatmap\">");
        sb.Append("<tr><th>Year</th>");
        for (var m = 1; m <= 12; m++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<th>{CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m)}</th>");
        }

        sb.AppendLine("<th>Annual</th></tr>");

        foreach (var year in years)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<tr><td><strong>{year}</strong></td>");
            var annualCompound = 1m;
            for (var m = 1; m <= 12; m++)
            {
                if (monthlyReturns.TryGetValue((year, m), out var ret))
                {
                    var cssClass = ret >= 0 ? "positive" : "negative";
                    sb.Append(CultureInfo.InvariantCulture, $"<td class=\"{cssClass}\">{ret:P1}</td>");
                    annualCompound *= (1 + ret);
                }
                else
                {
                    sb.Append("<td>—</td>");
                }
            }

            var annualReturn = annualCompound - 1m;
            var annClass = annualReturn >= 0 ? "positive" : "negative";
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"{annClass}\"><strong>{annualReturn:P1}</strong></td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string Encode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);
}
