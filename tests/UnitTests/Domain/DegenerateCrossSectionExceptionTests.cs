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
namespace Boutquin.Trading.Tests.UnitTests.Domain;

public sealed class DegenerateCrossSectionExceptionTests
{
    [Fact]
    public void Subtype_IsCalculationException()
    {
        var inner = new InvalidOperationException("inner");

        var parameterless = new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException();
        var withMessage = new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException("message");
        var withInner = new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException("outer", inner);

        Assert.IsAssignableFrom<Boutquin.Trading.Domain.Exceptions.CalculationException>(parameterless);
        Assert.Equal("message", withMessage.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void Subtype_IsCaughtByCalculationExceptionCatch()
    {
        Assert.True(IsCaughtByBaseType());

        static bool IsCaughtByBaseType()
        {
            try
            {
                throw new Boutquin.Trading.Domain.Exceptions.DegenerateCrossSectionException("degenerate");
            }
            catch (Boutquin.Trading.Domain.Exceptions.CalculationException)
            {
                return true;
            }
        }
    }
}
