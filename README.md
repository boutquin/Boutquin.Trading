# Boutquin.Trading

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)


A multi-asset, multi-strategy, event-driven trading platform for back testing strategies with portfolio-based risk management and %-per-strategy capital allocation.

## Overview

### Domain

This will contain all entities, enums, exceptions, interfaces, types and logic specific to the domain layer.

A key extension class here is the [DecimalArrayExtensions class](./doc/DecimalArrayExtensions.md), a static class that provides extension methods for working with arrays of decimal values. It includes methods for calculating the average, variance, and standard deviation of an array of decimal values, as well as the Sharpe Ratio and Annualized Sharpe Ratio of daily returns for a given array of decimal values.
