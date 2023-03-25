// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Custom exception for when the number of trading days per year is negative.
/// </summary>
public sealed class NegativeTradingDaysPerYearException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NegativeTradingDaysPerYearException"/> class.
    /// </summary>
    public NegativeTradingDaysPerYearException() : base(ExceptionMessages.NegativeTradingDaysPerYear)
    {
    }

    /// <summary>
    /// Constructor for the NegativeTradingDaysPerYearException class.
    /// </summary>
    /// <param name="message">The error message for the exception.</param>
    public NegativeTradingDaysPerYearException(string message) : base(message) 
    { 
    }
}
