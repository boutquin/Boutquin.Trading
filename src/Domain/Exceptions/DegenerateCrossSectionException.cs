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
namespace Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Represents a calculation failure caused by a cross-sectional statistic that is undefined because
/// every scored asset has the same value.
/// </summary>
/// <remarks>
/// This exception is reserved for a degeneracy across the scored universe. It must not represent a
/// per-asset degeneracy. An out-of-repository implementation that must surface an undefined per-asset
/// calculation should throw <see cref="CalculationException"/> directly.
/// </remarks>
public sealed class DegenerateCrossSectionException : CalculationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DegenerateCrossSectionException"/> class.
    /// </summary>
    public DegenerateCrossSectionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DegenerateCrossSectionException"/> class with the
    /// specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public DegenerateCrossSectionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DegenerateCrossSectionException"/> class with the
    /// specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public DegenerateCrossSectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
