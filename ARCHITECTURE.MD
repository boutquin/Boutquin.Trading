# Boutquin.Trading Solution Architecture

The Boutquin.Trading solution is an algorithmic trading system, organized into a set of projects each having a specific purpose. Below is an overview of the directory structure of the solution.

Boutquin.Trading
* Boutquin.Trading.Application
* Boutquin.Trading.BenchMark
* Boutquin.Trading.Data.AlphaVantage
* Boutquin.Trading.DataAccess
* Boutquin.Trading.Domain
* Boutquin.Trading.UnitTests
* Data
* doc
* Resources

## Boutquin.Trading.Application

This project is the entry point to the trading system, containing implementations of the strategies, event processors, risk managers, and portfolio simulations.

## Boutquin.Trading.BenchMark

This project is used for benchmarking the performance of the trading system, including the calculation speed and efficiency.

## Boutquin.Trading.Data.AlphaVantage

This project handles data fetching from the Alpha Vantage service, a provider of stock market data, forex data, and digital/crypto currency data.

## Boutquin.Trading.DataAccess

This project deals with the data access layer, including the storage and retrieval of trading data from the database.

## Boutquin.Trading.Domain

The domain project contains the core business logic, entities, and value objects of the trading system. It includes:

- `Data`: Handles market data processing and storage.
- `Entities`: Contains domain entities such as `AssetClass`, `Security`, `Position`, etc.
- `Enums`: Enumerations used across the solution, like `OrderType`, `TradeAction`, `RebalancingFrequency`, etc.
- `Events`: The events that drive the trading system, like `OrderEvent`, `FillEvent`, `MarketEvent`, etc.
- `Exceptions`: Custom exceptions for specific error scenarios in the trading system.
- `Extensions`: Contains extension methods for specific classes.
- `Helpers`: Provides helper classes for the trading system.
- `Interfaces`: Defines the contracts for various services, like `IBrokerage`, `ICapitalAllocationStrategy`, etc.

## Boutquin.Trading.UnitTests

This project is used for the unit testing of the Boutquin.Trading solution, ensuring the functionality of the system and avoiding regressions in the future.

## Data

This folder contains raw data that is used by the Boutquin.Trading system.

## doc

The `doc` folder includes markdown files that provide documentation for the Boutquin.Trading solution.

## Resources

This folder includes any resources used by the solution, such as images, fonts, etc. Currently, it contains the icon for the application.

## Root Files

- `.editorConfig`: Editor configuration used to maintain consistent coding styles across different editors and IDEs.
- `.gitattributes`: Git configuration file that defines the attributes per path.
- `.gitignore`: Specifies intentionally untracked files that Git should ignore.
- `Boutquin.Trading.sln`: The solution file that includes all the projects in the Boutquin.Trading solution.

Note: Each project contains a `.csproj` file which is a project file used by the .NET Core SDK's command-line interface (CLI) and Visual Studio. It includes information about the project, such as its type, target framework, and dependencies. Some projects also include a `doc` subfolder for project-specific documentation.