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

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Boutquin.Trading.DataAccess;

/// <summary>
/// A factory for creating instances of SecurityMasterContext at design-time.
/// </summary>
public sealed class SecurityMasterContextDesignTimeFactory : IDesignTimeDbContextFactory<SecurityMasterContext>
{
    /// <summary>
    /// Creates a new instance of the SecurityMasterContext.
    /// </summary>
    /// <param name="args">Command line arguments passed to the CreateDbContext method.</param>
    /// <returns>A new instance of SecurityMasterContext configured for use with a SQL Server database provider.</returns>
    public SecurityMasterContext CreateDbContext(string[] args)
    {
        // Get the current directory of the project
        var currentDirectory = Directory.GetCurrentDirectory();

        // Combine the current directory with the name of the configuration file
        var configurationFilePath = Path.Combine(currentDirectory, "appsettings.json");

        // Create a new instance of the ConfigurationBuilder class
        var configurationBuilder = new ConfigurationBuilder();

        // Add the JSON configuration file to the ConfigurationBuilder
        configurationBuilder.AddJsonFile(configurationFilePath);

        // Build the configuration
        var configuration = configurationBuilder.Build();

        // Get the connection string for the SQL Server database provider from the configuration
        var connectionString = configuration.GetConnectionString("SecurityMasterContext");

        // Configure the DbContextOptionsBuilder for the SecurityMasterContext
        var optionsBuilder = new DbContextOptionsBuilder<SecurityMasterContext>();

        // Use the SQL Server database provider
        optionsBuilder.UseSqlServer(connectionString);

        // Instantiate the SecurityMasterContext with the configured options
        return new SecurityMasterContext(optionsBuilder.Options);
    }
}
