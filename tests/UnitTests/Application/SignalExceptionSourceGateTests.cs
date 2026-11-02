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

using System.Text.RegularExpressions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed partial class SignalExceptionSourceGateTests
{
    [Fact]
    public void SignalSources_ContainOnlyTwoCrossSectionalCalculationExceptionThrows()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var signalsDirectory = Path.Combine(directory.FullName, "src", "Application", "Signals");
        Assert.True(Directory.Exists(signalsDirectory), $"Signal source directory not found: {signalsDirectory}");
        var sourceFiles = Directory.GetFiles(signalsDirectory, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.True(sourceFiles.Length >= 6, $"Expected at least six signal sources; found {sourceFiles.Length}.");

        var bareThrows = sourceFiles
            .SelectMany(file => BareCalculationExceptionThrow().Matches(File.ReadAllText(file)))
            .ToArray();
        Assert.Empty(bareThrows);

        var subtypeSites = sourceFiles
            .Select(file => (File: Path.GetFileName(file), Count: DegenerateCrossSectionThrow().Matches(File.ReadAllText(file)).Count))
            .Where(site => site.Count > 0)
            .ToArray();
        Assert.Equal(2, subtypeSites.Sum(site => site.Count));
        Assert.Equal(
            ["LowVolatilitySignal.cs", "ValueMomentumCompositeSignal.cs"],
            subtypeSites.Select(site => site.File).Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"throw\s+new\s+(?:Boutquin\.Trading\.Domain\.Exceptions\.)?CalculationException\s*\(")]
    private static partial Regex BareCalculationExceptionThrow();

    [GeneratedRegex(@"throw\s+new\s+(?:Boutquin\.Trading\.Domain\.Exceptions\.)?DegenerateCrossSectionException\s*\(")]
    private static partial Regex DegenerateCrossSectionThrow();
}
