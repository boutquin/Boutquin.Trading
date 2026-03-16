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

namespace Boutquin.Trading.Tests.ArchitectureTests;

// E1 fix: Replaced vacuous empty Test1() with a real architecture test
public class ApplicationAssemblyTests : BaseTest
{
    /// <summary>
    /// Verifies that all non-static classes in the Application assembly are sealed.
    /// </summary>
    [Fact]
    public void Classes_In_Application_Assembly_Should_Be_Sealed()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotStatic()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"the following types are not sealed: [{GetFailingTypes(result)}]");
    }
}
