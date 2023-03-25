# Class Name: DecimalArrayExtensions

The DecimalArrayExtensions class is a static class that provides extension methods for working with arrays of decimal values. It includes methods for calculating the average, variance, and standard deviation of an array of decimal values, as well as the Sharpe Ratio and Annualized Sharpe Ratio of daily returns for a given array of decimal values.

## Properties

- `CalculationType` (enum): an enum that represents the type of calculation for variance and standard deviation. It has two values: Sample and Population.

## Methods

### `SharpeRatio(this decimal[] dailyReturns, decimal riskFreeRate = 0m) -> decimal`

Calculates the Sharpe Ratio of daily returns for a given array of decimal values.

Throws an `EmptyOrNullArrayException` if the input array is null or empty, and an `InsufficientDataException` if the input array contains less than two elements for sample calculation.

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

Throws an `EmptyOrNullArrayException` if the input array is null or empty, and an `InsufficientDataException` if the input array contains less than two elements for sample calculation.

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

#### Constructor Parameters

- `message` (string): The exception message.

#### Example Usage

```csharp
throw new EmptyOrNullArrayException("Input array is null.");
```


### `InsufficientDataException`

Custom exception for insufficient data for a sample calculation.

#### Constructor Parameters

- `message` (string): The exception message.

#### Example Usage

```csharp
throw new InsufficientDataException("Not enough data for sample calculation.");
```


### `InvalidInputDataException`

Custom exception for invalid input data.

#### Constructor Parameters

- `message` (string): The exception message.

#### Example Usage

```csharp
throw new InvalidInputDataException("Invalid input data.");
```

## Inner Classes

### `ExceptionMessages`

Contains constants for exception messages.

#### Fields

- `EmptyOrNullArray` (string): Input array must not be empty or null.
- `InsufficientDataForSampleCalculation` (string): Input array must have at least two elements for sample calculation.

#### Example Usage

```csharp
public decimal Variance(this decimal[] values, CalculationType calculationType = CalculationType.Sample)
{
    if (values == null || values.Length == 0)
    {
        throw new EmptyOrNullArrayException(ExceptionMessages.EmptyOrNullArray);
    }

    if (calculationType == CalculationType.Sample && values.Length == 1)
    {
        throw new InsufficientDataException(ExceptionMessages.InsufficientDataForSampleCalculation);
    }

    var avg = values.Average();
    var sumOfSquares = values.Sum(x => (x - avg) * (x - avg));
    var denominator = calculationType == CalculationType.Sample ? values.Length - 1 : values.Length;
    return sumOfSquares / denominator;
}
```
