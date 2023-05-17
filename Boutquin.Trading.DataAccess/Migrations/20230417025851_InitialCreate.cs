#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Boutquin.Trading.DataAccess.Migrations
{
#nullable disable

    using Microsoft.EntityFrameworkCore.Migrations;

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
                    Id = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetClasses", x => x.Id);
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
                name: "SymbolStandards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolStandards", x => x.Id);
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
                    HolidayDate = table.Column<DateTime>(type: "Date", nullable: false),
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
                    AssetClassCode = table.Column<int>(type: "int", nullable: false),
                    ExchangeCode = table.Column<string>(type: "nvarchar(4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Securities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Securities_AssetClasses_AssetClassCode",
                        column: x => x.AssetClassCode,
                        principalTable: "AssetClasses",
                        principalColumn: "Id",
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
                    Standard = table.Column<int>(type: "int", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_SecuritySymbols_SymbolStandards_Standard",
                        column: x => x.Standard,
                        principalTable: "SymbolStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AssetClasses",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { 0, "Cash or Cash Equivalents" },
                    { 1, "Fixed Income Securities" },
                    { 2, "Equity Securities" },
                    { 3, "Real Estate" },
                    { 4, "Commodities" },
                    { 5, "Alternative Investments" },
                    { 6, "Crypto-Currencies" },
                    { 7, "Other" }
                });

            migrationBuilder.InsertData(
                table: "Continents",
                columns: new[] { "Code", "Name" },
                values: new object[,]
                {
                    { "AF", "Africa" },
                    { "AN", "Antarctica" },
                    { "AS", "Asia" },
                    { "EU", "Europe" },
                    { "NA", "North America" },
                    { "OC", "Oceania" },
                    { "SA", "South America" }
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Code", "Name", "NumericCode", "Symbol" },
                values: new object[,]
                {
                    { "AUD", "Australian dollar", 36, "$" },
                    { "BRL", "Brazilian real", 986, "R$" },
                    { "CAD", "Canadian dollar", 124, "$" },
                    { "CNY", "Chinese yuan", 156, "¥" },
                    { "EUR", "Euro", 978, "€" },
                    { "GBP", "British pound", 826, "£" },
                    { "HKD", "Hong Kong dollar", 344, "HK$" },
                    { "INR", "Indian rupee", 356, "₹" },
                    { "JPY", "Japanese yen", 392, "¥" },
                    { "KRW", "South Korean won", 410, "₩" },
                    { "MXN", "Mexican peso", 484, "$" },
                    { "RUB", "Russian ruble", 643, "₽" },
                    { "USD", "United States dollar", 840, "$" }
                });

            migrationBuilder.InsertData(
                table: "SymbolStandards",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { 0, "CUSIP" },
                    { 1, "ISIN" },
                    { 2, "SEDOL" },
                    { 3, "RIC" },
                    { 4, "Bloomberg Ticker" }
                });

            migrationBuilder.InsertData(
                table: "TimeZones",
                columns: new[] { "Code", "Name", "TimeZoneOffset", "UsesDaylightSaving" },
                values: new object[,]
                {
                    { "AEST", "Australian Eastern Standard Time", "+10:00", false },
                    { "CET", "Central European Time", "+01:00", false },
                    { "CST", "China Standard Time", "+08:00", false },
                    { "EST", "Eastern Standard Time", "-05:00", false },
                    { "GMT", "Greenwich Mean Time", "GMT", false },
                    { "HKT", "Hong Kong Time", "+08:00", false },
                    { "JST", "Japan Standard Time", "+09:00", false },
                    { "MSK", "Moscow Standard Time", "+04:00", false },
                    { "UTC", "Coordinated Universal Time", "Z", false }
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Code", "ContinentCode", "CurrencyCode", "Name", "NumericCode" },
                values: new object[,]
                {
                    { "CA", "NA", "CAD", "Canada", 124 },
                    { "CN", "AS", "CNY", "China", 156 },
                    { "DE", "EU", "EUR", "Germany", 276 },
                    { "FR", "EU", "EUR", "France", 250 },
                    { "GB", "EU", "GBP", "United Kingdom", 826 },
                    { "HK", "AS", "HKD", "Hong Kong", 344 },
                    { "IN", "AS", "INR", "India", 356 },
                    { "JP", "AS", "JPY", "Japan", 392 },
                    { "KR", "AS", "KRW", "South Korea", 410 },
                    { "RU", "EU", "RUB", "Russia", 643 },
                    { "US", "NA", "USD", "United States", 840 }
                });

            migrationBuilder.InsertData(
                table: "Cities",
                columns: new[] { "Id", "CountryCode", "Name", "TimeZoneCode" },
                values: new object[,]
                {
                    { 1, "US", "New York", "UTC" },
                    { 2, "JP", "Tokyo", "JST" },
                    { 3, "CN", "Shanghai", "CST" },
                    { 4, "HK", "Hong Kong", "HKT" },
                    { 5, "FR", "Paris", "CET" },
                    { 6, "GB", "London", "GMT" },
                    { 7, "DE", "Frankfurt", "CET" },
                    { 8, "RU", "Moscow", "MSK" },
                    { 9, "CA", "Toronto", "EST" }
                });

            migrationBuilder.InsertData(
                table: "Exchanges",
                columns: new[] { "Code", "CityId", "Name" },
                values: new object[,]
                {
                    { "XETR", 7, "Deutsche Boerse XETRA" },
                    { "XHKG", 4, "Hong Kong Stock Exchange" },
                    { "XLON", 6, "London Stock Exchange" },
                    { "XMOS", 8, "Moscow Exchange" },
                    { "XNAS", 1, "NASDAQ Stock Market" },
                    { "XNYS", 1, "New York Stock Exchange" },
                    { "XPAR", 5, "Euronext Paris" },
                    { "XSHG", 3, "Shanghai Stock Exchange" },
                    { "XTOR", 9, "Toronto Stock Exchange" },
                    { "XTSE", 2, "Tokyo Stock Exchange" }
                });

            migrationBuilder.InsertData(
                table: "ExchangeSchedules",
                columns: new[] { "Id", "CloseTime", "DayOfWeek", "ExchangeCode", "OpenTime" },
                values: new object[,]
                {
                    { 1, new TimeSpan(0, 16, 0, 0, 0), 1, "XNYS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 2, new TimeSpan(0, 16, 0, 0, 0), 2, "XNYS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 3, new TimeSpan(0, 16, 0, 0, 0), 3, "XNYS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 4, new TimeSpan(0, 16, 0, 0, 0), 4, "XNYS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 5, new TimeSpan(0, 16, 0, 0, 0), 5, "XNYS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 6, new TimeSpan(0, 16, 0, 0, 0), 1, "XNAS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 7, new TimeSpan(0, 16, 0, 0, 0), 2, "XNAS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 8, new TimeSpan(0, 16, 0, 0, 0), 3, "XNAS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 9, new TimeSpan(0, 16, 0, 0, 0), 4, "XNAS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 10, new TimeSpan(0, 16, 0, 0, 0), 5, "XNAS", new TimeSpan(0, 9, 30, 0, 0) },
                    { 11, new TimeSpan(0, 15, 0, 0, 0), 1, "XTSE", new TimeSpan(0, 9, 0, 0, 0) },
                    { 12, new TimeSpan(0, 15, 0, 0, 0), 2, "XTSE", new TimeSpan(0, 9, 0, 0, 0) },
                    { 13, new TimeSpan(0, 15, 0, 0, 0), 3, "XTSE", new TimeSpan(0, 9, 0, 0, 0) },
                    { 14, new TimeSpan(0, 15, 0, 0, 0), 4, "XTSE", new TimeSpan(0, 9, 0, 0, 0) },
                    { 15, new TimeSpan(0, 15, 0, 0, 0), 5, "XTSE", new TimeSpan(0, 9, 0, 0, 0) },
                    { 16, new TimeSpan(0, 15, 0, 0, 0), 1, "XSHG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 17, new TimeSpan(0, 15, 0, 0, 0), 2, "XSHG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 18, new TimeSpan(0, 15, 0, 0, 0), 3, "XSHG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 19, new TimeSpan(0, 15, 0, 0, 0), 4, "XSHG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 20, new TimeSpan(0, 15, 0, 0, 0), 5, "XSHG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 21, new TimeSpan(0, 16, 0, 0, 0), 1, "XHKG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 22, new TimeSpan(0, 16, 0, 0, 0), 2, "XHKG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 23, new TimeSpan(0, 16, 0, 0, 0), 3, "XHKG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 24, new TimeSpan(0, 16, 0, 0, 0), 4, "XHKG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 25, new TimeSpan(0, 16, 0, 0, 0), 5, "XHKG", new TimeSpan(0, 9, 30, 0, 0) },
                    { 26, new TimeSpan(0, 17, 30, 0, 0), 1, "XPAR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 27, new TimeSpan(0, 17, 30, 0, 0), 2, "XPAR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 28, new TimeSpan(0, 17, 30, 0, 0), 3, "XPAR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 29, new TimeSpan(0, 17, 30, 0, 0), 4, "XPAR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 30, new TimeSpan(0, 17, 30, 0, 0), 5, "XPAR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 31, new TimeSpan(0, 16, 30, 0, 0), 1, "XLON", new TimeSpan(0, 8, 0, 0, 0) },
                    { 32, new TimeSpan(0, 16, 30, 0, 0), 2, "XLON", new TimeSpan(0, 8, 0, 0, 0) },
                    { 33, new TimeSpan(0, 16, 30, 0, 0), 3, "XLON", new TimeSpan(0, 8, 0, 0, 0) },
                    { 34, new TimeSpan(0, 16, 30, 0, 0), 4, "XLON", new TimeSpan(0, 8, 0, 0, 0) },
                    { 35, new TimeSpan(0, 16, 30, 0, 0), 5, "XLON", new TimeSpan(0, 8, 0, 0, 0) },
                    { 36, new TimeSpan(0, 17, 30, 0, 0), 1, "XETR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 37, new TimeSpan(0, 17, 30, 0, 0), 2, "XETR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 38, new TimeSpan(0, 17, 30, 0, 0), 3, "XETR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 39, new TimeSpan(0, 17, 30, 0, 0), 4, "XETR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 40, new TimeSpan(0, 17, 30, 0, 0), 5, "XETR", new TimeSpan(0, 9, 0, 0, 0) },
                    { 41, new TimeSpan(0, 18, 45, 0, 0), 1, "XMOS", new TimeSpan(0, 10, 0, 0, 0) },
                    { 42, new TimeSpan(0, 18, 45, 0, 0), 2, "XMOS", new TimeSpan(0, 10, 0, 0, 0) },
                    { 43, new TimeSpan(0, 18, 45, 0, 0), 3, "XMOS", new TimeSpan(0, 10, 0, 0, 0) },
                    { 44, new TimeSpan(0, 18, 45, 0, 0), 4, "XMOS", new TimeSpan(0, 10, 0, 0, 0) },
                    { 45, new TimeSpan(0, 18, 45, 0, 0), 5, "XMOS", new TimeSpan(0, 10, 0, 0, 0) },
                    { 46, new TimeSpan(0, 16, 0, 0, 0), 1, "XTOR", new TimeSpan(0, 9, 30, 0, 0) },
                    { 47, new TimeSpan(0, 16, 0, 0, 0), 2, "XTOR", new TimeSpan(0, 9, 30, 0, 0) },
                    { 48, new TimeSpan(0, 16, 0, 0, 0), 3, "XTOR", new TimeSpan(0, 9, 30, 0, 0) },
                    { 49, new TimeSpan(0, 16, 0, 0, 0), 4, "XTOR", new TimeSpan(0, 9, 30, 0, 0) },
                    { 50, new TimeSpan(0, 16, 0, 0, 0), 5, "XTOR", new TimeSpan(0, 9, 30, 0, 0) }
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
                name: "IX_SecuritySymbols_Standard",
                table: "SecuritySymbols",
                column: "Standard");

            migrationBuilder.CreateIndex(
                name: "IX_SymbolStandards_Description",
                table: "SymbolStandards",
                column: "Description",
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
                name: "SymbolStandards");

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
