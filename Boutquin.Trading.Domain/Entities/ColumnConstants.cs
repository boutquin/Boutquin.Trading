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

namespace Boutquin.Trading.Domain.Entities;

/// <summary>
/// Contains the string lengths, precision, and scale of all columns from the database schema.
/// </summary>
public static class ColumnConstants
{
    /// <summary>
    /// The name of the porimary key column in a table.
    /// </summary>
    public const string Default_Primary_Key_Name = "Id";

    // TimeZone table
    /// <summary>
    /// The length of the Code column in the TimeZone table.
    /// </summary>
    public const int TimeZone_Code_Length = 10;

    /// <summary>
    /// The length of the Name column in the TimeZone table.
    /// </summary>
    public const int TimeZone_Name_Length = 50;

    /// <summary>
    /// The length of the TimeZoneOffset column in the TimeZone table.
    /// </summary>
    public const int TimeZone_TimeZoneOffset_Length = 6;

    // AssetClass table
    /// <summary>
    /// The length of the Code column in the AssetClass table.
    /// </summary>
    public const int AssetClass_Code_Length = 3;

    /// <summary>
    /// The length of the Description column in the AssetClass table.
    /// </summary>
    public const int AssetClass_Description_Length = 50;

    // Continent table
    /// <summary>
    /// The length of the Code column in the Continent table.
    /// </summary>
    public const int Continent_Code_Length = 2;

    /// <summary>
    /// The length of the Name column in the Continent table.
    /// </summary>
    public const int Continent_Name_Length = 50;

    // Country table
    /// <summary>
    /// The length of the Code column in the Country table.
    /// </summary>
    public const int Country_Code_Length = 2;

    /// <summary>
    /// The length of the CurrencyCode column in the Country table.
    /// </summary>
    public const int Country_CurrencyCode_Length = Currency_Code_Length;

    /// <summary>
    /// The length of the ContinentCode column in the Country table.
    /// </summary>
    public const int Country_ContinentCode_Length = Continent_Code_Length;


    /// <summary>
    /// The length of the Name column in the Country table.
    /// </summary>
    public const int Country_Name_Length = 50;

    // Currency table
    /// <summary>
    /// The length of the Code column in the Currency table.
    /// </summary>
    public const int Currency_Code_Length = 3;

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
    /// The length of the Code column in the Exchange table.
    /// </summary>
    public const int Exchange_Code_Length = 4;

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
    /// The length of the Name column in the Security table.
    /// </summary>
    public const int Security_Name_Length = 200;

    /// <summary>
    /// The length of the ExchangeCode column in the Security table.
    /// </summary>
    public const int Security_ExchangeCode_Length = Exchange_Code_Length;

    /// <summary>
    /// The length of the AssetClassCode  column in the Security table.
    /// </summary>
    public const int Security_AssetClassCode_Length = AssetClass_Code_Length;

    // SecuritySymbol table
    /// <summary>
    /// The length of the Symbol column in the SecuritySymbol table.
    /// </summary>
    public const int SecuritySymbol_Symbol_Length = 50;

    /// <summary>
    /// The length of the Symbol column in the SecuritySymbol table.
    /// </summary>
    public const int SecuritySymbol_Standard_Length = 50;

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
    /// The length of the BaseCurrencyCode column in the FxRate table.
    /// </summary>
    public const int FxRate_BaseCurrencyCode_Length = Currency_Code_Length;

    /// <summary>
    /// The length of the QuoteCurrencyCode column in the FxRate table.
    /// </summary>
    public const int FxRate_QuoteCurrencyCode_Length = Currency_Code_Length;

    /// <summary>
    /// The precision of the Rate column in the FxRate table.
    /// </summary>
    public const int FxRate_Rate_Precision = 18;

    /// <summary>
    /// The scale of the Rate column in the FxRate table.
    /// </summary>
    public const int FxRate_Rate_Scale = 6;

    // ExchangeHoliday table
    /// <summary>
    /// The length of the Description column in the ExchangeHoliday table.
    /// </summary>
    public const int ExchangeHoliday_Description_Length = 50;

    /// <summary>
    /// The length of the ExchangeCode column in the ExchangeHoliday table.
    /// </summary>
    public const int ExchangeHoliday_ExchangeCode_Length = Exchange_Code_Length;

    // ExchangeSchedule table
    /// <summary>
    /// The length of the ExchangeCode column in the ExchangeSchedule table.
    /// </summary>
    public const int ExchangeSchedule_ExchangeCode_Length = Exchange_Code_Length;

    // City table
    /// <summary>
    /// The length of the Name column in the City table.
    /// </summary>
    public const int City_Name_Length = 50;

    /// <summary>
    /// The length of the TimeZoneCode column in the City table.
    /// </summary>
    public const int City_TimeZoneCode_Length = TimeZone_Code_Length;

    /// <summary>
    /// The length of the CountryCode column in the City table.
    /// </summary>
    public const int City_CountryCode_Length = Country_Code_Length;

}
