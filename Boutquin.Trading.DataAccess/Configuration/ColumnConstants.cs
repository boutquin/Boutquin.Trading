// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// Contains the string lengths, precision, and scale of all columns from the database schema.
/// </summary>
public static class ColumnConstants
{
    // TimeZone table
    /// <summary>
    /// The length of the Name column in the TimeZone table.
    /// </summary>
    public const int TimeZone_Name_Length = 50;

    /// <summary>
    /// The length of the Abbreviation column in the TimeZone table.
    /// </summary>
    public const int TimeZone_Abbreviation_Length = 10;

    /// <summary>
    /// The length of the TimeZoneOffset column in the TimeZone table.
    /// </summary>
    public const int TimeZone_TimeZoneOffset_Length = 6;

    // AssetClass table
    /// <summary>
    /// The length of the Name column in the AssetClass table.
    /// </summary>
    public const int AssetClass_Name_Length = 50;

    /// <summary>
    /// The length of the Description column in the AssetClass table.
    /// </summary>
    public const int AssetClass_Description_Length = 200;

    // Continent table
    /// <summary>
    /// The length of the Name column in the Continent table.
    /// </summary>
    public const int Continent_Name_Length = 50;

    // Country table
    /// <summary>
    /// The length of the Name column in the Country table.
    /// </summary>
    public const int Country_Name_Length = 50;

    // Currency table
    /// <summary>
    /// The length of the Name column in the Currency table.
    /// </summary>
    public const int Currency_Name_Length = 50;

    /// <summary>
    /// The length of the Symbol column in the Currency table.
    /// </summary>
    public const int Currency_Symbol_Length = 5;

    // Exchange table
    /// <summary>
    /// The length of the Name column in the Exchange table.
    /// </summary>
    public const int Exchange_Name_Length = 50;

    /// <summary>
    /// The length of the City column in the Exchange table.
    /// </summary>
    public const int Exchange_City_Length = 50;

    // Security table
    /// <summary>
    /// The length of the Symbol column in the Security table.
    /// </summary>
    public const int Security_Symbol_Length = 50;

    /// <summary>
    /// The length of the Name column in the Security table.
    /// </summary>
    public const int Security_Name_Length = 200;

    // SecurityPrice table
    /// <summary>
    /// The precision of the OpenPrice, HighPrice, LowPrice, ClosePrice, and Dividend columns in the SecurityPrice table.
    /// </summary>
    public const int SecurityPrice_Price_Precision = 18;

    /// <summary>
    /// The scale of the OpenPrice, HighPrice, LowPrice, ClosePrice, and Dividend columns in the SecurityPrice table.
    /// </summary>
    public const int SecurityPrice_Price_Scale = 2;

    // FxRate table
    /// <summary>
    /// The precision of the Rate column in the FxRate table.
    /// </summary>
    public const int FxRate_Rate_Precision = 18;

    /// <summary>
    /// The scale of the Rate column in the FxRate table.
    /// </summary>
    public const int FxRate_Rate_Scale = 6;
}
