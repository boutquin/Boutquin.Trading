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
namespace Boutquin.Trading.Tests.ArchitectureTests;

using Boutquin.Domain.Abstractions;

/// <summary>
/// Contains tests for ensuring the architectural integrity of the domain assembly.
/// These tests verify that classes and domain events within the domain assembly adhere to
/// specific design principles and naming conventions.
/// </summary>
public sealed class DomainAssemblyTests : BaseTest
{
    /// <summary>
    /// Verifies that all non-static classes in the domain assembly are sealed.
    /// This test ensures that the domain classes are not inheritable, promoting composition over inheritance
    /// and maintaining encapsulation within the domain model.
    /// </summary>
    [Fact]
    public void Classes_In_Domain_Assembly_Should_Be_Sealed()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotStatic()
            .Should()
            .BeSealed()
            .GetResult();

        GetFailingTypes(result).Should().BeEmpty();
    }

    /// <summary>
    /// Ensures that all implementations of the IDomainEvent interface have a name ending with 'DomainEvent'.
    /// This test checks for consistent naming conventions in domain events, which helps in identifying them
    /// easily within the codebase and ensures clarity in their purpose.
    /// </summary>
    [Fact]
    public void DomainEvent_Should_Have_DomainEventPostfix()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should().HaveNameEndingWith("DomainEvent")
            .GetResult();

        GetFailingTypes(result).Should().BeEmpty();
    }

    /// <summary>
    /// Ensures that all interfaces have a name starting with 'I'.
    /// This test checks for consistent naming conventions in interfaces, which helps in identifying them
    /// easily within the codebase and ensures clarity in their purpose.
    /// </summary>
    [Fact]
    public void Interfaces_Should_Have_Name_Starting_With_I()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .Should().HaveNameStartingWith("I")
            .GetResult();

        GetFailingTypes(result).Should().BeEmpty();
    }
}
