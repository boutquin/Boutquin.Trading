// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.Tests.ArchitectureTests.Helpers;

using Application;

using DataAccess;

using Domain.Interfaces;

/// <summary>
/// Base class for architecture tests, providing common functionalities for testing.
/// Includes utility methods to aid in asserting conditions and retrieving information about test results.
/// </summary>
public class BaseTest
{
    protected static Assembly DomainAssembly => typeof(IPositionSizer).Assembly;

    protected static Assembly ApplicationAssembly => typeof(BackTest).Assembly;

    protected static Assembly PersistenceAssembly => typeof(SecurityMasterContext).Assembly;

    /// <summary>
    /// Retrieves a comma-separated list of the names of types that failed a given test result.
    /// </summary>
    /// <param name="result">The result object of the test.</param>
    /// <returns>A string containing the names of the failing types. Returns an empty string if there are no failing types.</returns>
    /// <example>
    /// // Example of using GetFailingTypes in a unit test assertion
    /// [Fact]
    /// public void TestArchitecture()
    /// {
    ///     var result = //... perform some architecture test ...;
    ///     var failingTypes = GetFailingTypes(result);
    ///     Assert.True(result.IsSuccessful, $"The following types failed: [{GetFailingTypes(result)}].");
    ///     // FluentAssertions usage for asserting the test result
    ///     GetFailingTypes(result).Should().BeEmpty();
    /// }
    /// </example>
    protected string GetFailingTypes(TestResult result)
    {
        return result.FailingTypes != null ?
            string.Join(", ", result.FailingTypes.Select(type => type.Name)) :
            string.Empty;
    }
}
