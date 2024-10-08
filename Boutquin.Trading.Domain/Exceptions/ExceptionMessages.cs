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
namespace Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Contains constants for exception messages.
/// </summary>
public static class ExceptionMessages
{
    public const string NegativeTradingDaysPerYear = "The number of trading days per year must be positive.";
    //public const string InsufficientDataForSampleCalculation = "Insufficient data to calculate the sample mean and standard deviation.";
}
