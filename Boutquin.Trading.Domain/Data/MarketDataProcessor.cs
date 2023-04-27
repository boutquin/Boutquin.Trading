﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Data;

public sealed class MarketDataProcessor
{
    private readonly IMarketDataReader _marketDataReader;
    private readonly IMarketDataWriter _marketDataWriter;

    public MarketDataProcessor(IMarketDataReader marketDataReader, IMarketDataWriter marketDataWriter)
    {
        _marketDataReader = marketDataReader;
        _marketDataWriter = marketDataWriter;
    }

    public async Task Process(IEnumerable<string> assets, DateOnly startDate, DateOnly endDate)
    {
        var marketData = await _marketDataReader.LoadHistoricalMarketDataAsync(assets, startDate, endDate);
        await _marketDataWriter.SaveHistoricalMarketDataAsync(marketData);

        var dividendData = await _marketDataReader.LoadHistoricalDividendDataAsync(assets, startDate, endDate);
        await _marketDataWriter.SaveHistoricalDividendDataAsync(dividendData);
    }
}
