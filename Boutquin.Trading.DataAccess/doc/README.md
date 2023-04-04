# Boutquin.Trading.DataAccess

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.dataaccess?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

*** Very much a work in progress ***

# Security Master Data Access Layer

This repository contains the data access layer for the Security Master system. 

It includes configuration classes and a `SecurityMasterDbContext` that encompasses the following 13 entities:

1. AssetClass
2. City
3. Continent
4. Country
5. Currency
6. Exchange
7. ExchangeHoliday
8. ExchangeSchedule
9. FxRate
10. Security
11. SecurityPrice
12. SecuritySymbol
13. TimeZone

## Getting Started

To use the Security Master Data Access Layer, follow these steps:

1. Clone the repository to your local machine.
2. Open the solution in your favorite IDE (e.g., Visual Studio).
3. Set up the appropriate connection string in the `appsettings.json` file to connect to your preferred database system.
4. Run any necessary database migrations using the built-in Entity Framework Core tools.

## Configuration Classes

The configuration classes are responsible for configuring the entity classes, setting up relationships, and ensuring the correct column types, lengths, and constraints are applied to the database tables.

## SecurityMasterDbContext

`SecurityMasterDbContext` is the main `DbContext` class that encompasses all the 13 entities. It is responsible for creating and managing the database connections and transactions, and it enables the use of LINQ queries to interact with the data.

## Contributing

If you'd like to contribute to the development of the Security Master Data Access Layer, please feel free to submit a pull request or open an issue with your suggestions or improvements.

## License

This project is licensed under the Apache 2.0 License. See the LICENSE file for more information.
