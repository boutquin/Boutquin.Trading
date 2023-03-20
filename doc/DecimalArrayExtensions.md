# Class Name: DecimalArrayExtensions

The DecimalArrayExtensions class is a static class that provides extension methods for working with arrays of decimal values. It includes methods for calculating the average, variance, and standard deviation of an array of decimal values, as well as the Sharpe Ratio and Annualized Sharpe Ratio of daily returns for a given array of decimal values.

## Properties

- `CalculationType` (enum): an enum that represents the type of calculation for variance and standard deviation. It has two values: Sample and Population.

## Methods

### `Average(this decimal[] values) -> decimal`

Calculates the average of an array of decimal values.

Throws an `InvalidInputDataException` if the input array is empty or null.

#### Parameters

- `values` (decimal[]): The array of decimal values.

#### Returns

(decimal): The average of the values.

#### Example Usage

```csharp
decimal[] values = { 1.5m, 2.0m, 3.5m, 4.2m, 5.8m };
decimal average = values.Average();
```


### `Variance(this decimal[] values, CalculationType calculationType = CalculationType.Sample) -> decimal`

Calculates the variance of an array of decimal values.

Throws an `EmptyOrNullArrayException` if the input array is empty, and an `InsufficientDataException` if the input array contains less than two elements for sample calculation.

#### Parameters

- `values` (decimal[]): The array of decimal values.
- `calculationType` (CalculationType, optional): The type of calculation (sample or population). Defaults to CalculationType.Sample.

#### Returns

(decimal): The variance of the values.

#### Example Usage

```csharp
decimal[] values = { 1.5m, 2.0m, 3.5m, 4.2m, 5.8m };
decimal variance = values.Variance();
```


### `StandardDeviation(this decimal[] values, CalculationType calculationType = CalculationType.Sample) -> decimal`

Calculates the standard deviation of an array of decimal values.

Throws an `EmptyOrNullArrayException` if the input array is empty, and an `InsufficientDataException` if the input array contains less than two elements for sample calculation.

#### Parameters

- `values` (decimal[]): The array of decimal values.
- `calculationType` (CalculationType, optional): The type of calculation (sample or population). Defaults to CalculationType.Sample.

#### Returns

(decimal): The standard deviation of the values.

#### Example Usage

```csharp
decimal[] values = { 1.5m, 2.0m, 3.5m, 4.2m, 5.8m };
decimal std_deviation = values.StandardDeviation();
```


### `SharpeRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m) -> decimal`

Calculates the Sharpe Ratio of daily returns for a given array of decimal values.

#### Parameters

- `dailyReturns` (decimal[]): An array of daily returns.
- `riskFreeRate` (decimal, optional): The risk-free rate, expressed as a daily value. Defaults to 0.

#### Returns

(decimal): The Sharpe Ratio.

#### Example Usage

```csharp
decimal[] daily_returns = { 0.01m, 0.02m, 0.03m, -0.01m, -0.02m, 0.01m };
decimal sharpe_ratio = daily_returns.SharpeRatio();
```


### `AnnualizedSharpeRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m, int tradingDaysPerYear = 252) -> decimal`

Calculates the Annualized Sharpe Ratio of daily returns for a given array of decimal values.

#### Parameters

- `dailyReturns` (decimal[]): An array of daily returns.
- `riskFreeRate` (decimal, optional): The risk-free rate, expressed as a daily value. Defaults to 0.
- `tradingDaysPerYear` (int, optional): The number of trading days per year. Defaults to 252.

#### Returns

(decimal): The Annualized Sharpe Ratio.

#### Example Usage

```csharp
decimal[] daily_returns = { 0.01m, 0.02m, 0.03m, -0.01m, -0.02m, 0.01m };
decimal annualized_sharpe_ratio = daily_returns.AnnualizedSharpeRatio();
```

## Exceptions

### `EmptyOrNullArrayException`

Custom exception for an empty or null input array.


### `InsufficientDataException`

Custom exception for insufficient data for a sample calculation.


### `InvalidInputDataException`

Custom exception for invalid input data.
