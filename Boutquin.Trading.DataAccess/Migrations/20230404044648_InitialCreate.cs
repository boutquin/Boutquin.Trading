using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boutquin.Trading.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetClasses",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetClasses", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Continents",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Continents", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NumericCode = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TimeZones",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeZoneOffset = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    UsesDaylightSaving = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZones", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NumericCode = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ContinentCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Countries_Continents_ContinentCode",
                        column: x => x.ContinentCode,
                        principalTable: "Continents",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Countries_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RateDate = table.Column<DateTime>(type: "Date", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRates_Currencies_BaseCurrencyCode",
                        column: x => x.BaseCurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FxRates_Currencies_QuoteCurrencyCode",
                        column: x => x.QuoteCurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeZoneCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cities_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cities_TimeZones_TimeZoneCode",
                        column: x => x.TimeZoneCode,
                        principalTable: "TimeZones",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Exchanges",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchanges", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Exchanges_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExchangeCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeHolidays_Exchanges_ExchangeCode",
                        column: x => x.ExchangeCode,
                        principalTable: "Exchanges",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExchangeCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeSchedules_Exchanges_ExchangeCode",
                        column: x => x.ExchangeCode,
                        principalTable: "Exchanges",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Securities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssetClassCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeCode = table.Column<string>(type: "nvarchar(4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Securities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Securities_AssetClasses_AssetClassCode",
                        column: x => x.AssetClassCode,
                        principalTable: "AssetClasses",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Securities_Exchanges_ExchangeCode",
                        column: x => x.ExchangeCode,
                        principalTable: "Exchanges",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecurityPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeDate = table.Column<DateTime>(type: "Date", nullable: false),
                    SecurityId = table.Column<int>(type: "int", nullable: false),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HighPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LowPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Volume = table.Column<int>(type: "int", nullable: false),
                    Dividend = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityPrices_Securities_SecurityId",
                        column: x => x.SecurityId,
                        principalTable: "Securities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecuritySymbols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SecurityId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuritySymbols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecuritySymbols_Securities_SecurityId",
                        column: x => x.SecurityId,
                        principalTable: "Securities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetClasses_Description",
                table: "AssetClasses",
                column: "Description",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CountryCode_Name",
                table: "Cities",
                columns: new[] { "CountryCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_TimeZoneCode",
                table: "Cities",
                column: "TimeZoneCode");

            migrationBuilder.CreateIndex(
                name: "IX_Continents_Name",
                table: "Continents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_ContinentCode",
                table: "Countries",
                column: "ContinentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CurrencyCode",
                table: "Countries",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Name",
                table: "Countries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Name",
                table: "Currencies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_NumericCode",
                table: "Currencies",
                column: "NumericCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeHolidays_Description",
                table: "ExchangeHolidays",
                column: "Description",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeHolidays_ExchangeCode_HolidayDate",
                table: "ExchangeHolidays",
                columns: new[] { "ExchangeCode", "HolidayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_CityId",
                table: "Exchanges",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_Name",
                table: "Exchanges",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeSchedules_ExchangeCode_DayOfWeek",
                table: "ExchangeSchedules",
                columns: new[] { "ExchangeCode", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_BaseCurrencyCode",
                table: "FxRates",
                column: "BaseCurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_QuoteCurrencyCode",
                table: "FxRates",
                column: "QuoteCurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_RateDate_BaseCurrencyCode_QuoteCurrencyCode",
                table: "FxRates",
                columns: new[] { "RateDate", "BaseCurrencyCode", "QuoteCurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Securities_AssetClassCode",
                table: "Securities",
                column: "AssetClassCode");

            migrationBuilder.CreateIndex(
                name: "IX_Securities_ExchangeCode",
                table: "Securities",
                column: "ExchangeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Securities_Name",
                table: "Securities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityPrices_SecurityId",
                table: "SecurityPrices",
                column: "SecurityId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityPrices_TradeDate_SecurityId",
                table: "SecurityPrices",
                columns: new[] { "TradeDate", "SecurityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecuritySymbols_SecurityId_Standard",
                table: "SecuritySymbols",
                columns: new[] { "SecurityId", "Standard" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeZones_Name",
                table: "TimeZones",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeHolidays");

            migrationBuilder.DropTable(
                name: "ExchangeSchedules");

            migrationBuilder.DropTable(
                name: "FxRates");

            migrationBuilder.DropTable(
                name: "SecurityPrices");

            migrationBuilder.DropTable(
                name: "SecuritySymbols");

            migrationBuilder.DropTable(
                name: "Securities");

            migrationBuilder.DropTable(
                name: "AssetClasses");

            migrationBuilder.DropTable(
                name: "Exchanges");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "TimeZones");

            migrationBuilder.DropTable(
                name: "Continents");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
