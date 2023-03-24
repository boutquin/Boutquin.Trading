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
/// Custom exception for when the risk-free rate is negative.
/// </summary>
public sealed class NegativeRiskFreeRateException : Exception
{
    /// <summary>
    /// Constructor for the NegativeRiskFreeRateException class.
    /// </summary>
    /// <param name="message">The error message for the exception.</param>
    public NegativeRiskFreeRateException(string message) : base(message) { }
}
