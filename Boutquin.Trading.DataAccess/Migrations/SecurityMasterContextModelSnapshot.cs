﻿// <auto-generated />
using System;
using Boutquin.Trading.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Boutquin.Trading.DataAccess.Migrations
{
    [DbContext(typeof(SecurityMasterContext))]
    partial class SecurityMasterContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.AssetClass", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.HasIndex("Description")
                        .IsUnique();

                    b.ToTable("AssetClasses");

                    b.HasData(
                        new
                        {
                            Id = 0,
                            Description = "Cash or Cash Equivalents"
                        },
                        new
                        {
                            Id = 1,
                            Description = "Fixed Income Securities"
                        },
                        new
                        {
                            Id = 2,
                            Description = "Equity Securities"
                        },
                        new
                        {
                            Id = 3,
                            Description = "Real Estate"
                        },
                        new
                        {
                            Id = 4,
                            Description = "Commodities"
                        },
                        new
                        {
                            Id = 5,
                            Description = "Alternative Investments"
                        },
                        new
                        {
                            Id = 6,
                            Description = "Crypto-Currencies"
                        },
                        new
                        {
                            Id = 7,
                            Description = "Other"
                        });
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.City", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<string>("CountryCode")
                        .IsRequired()
                        .HasMaxLength(2)
                        .HasColumnType("nvarchar(2)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("TimeZoneCode")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.HasKey("_id");

                    b.HasIndex("TimeZoneCode");

                    b.HasIndex("CountryCode", "Name")
                        .IsUnique();

                    b.ToTable("Cities");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Continent", b =>
                {
                    b.Property<string>("Code")
                        .HasMaxLength(2)
                        .HasColumnType("nvarchar(2)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Code");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Continents");

                    b.HasData(
                        new
                        {
                            Code = "AF",
                            Name = "Africa"
                        },
                        new
                        {
                            Code = "AN",
                            Name = "Antarctica"
                        },
                        new
                        {
                            Code = "AS",
                            Name = "Asia"
                        },
                        new
                        {
                            Code = "EU",
                            Name = "Europe"
                        },
                        new
                        {
                            Code = "NA",
                            Name = "North America"
                        },
                        new
                        {
                            Code = "OC",
                            Name = "Oceania"
                        },
                        new
                        {
                            Code = "SA",
                            Name = "South America"
                        });
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Country", b =>
                {
                    b.Property<string>("Code")
                        .HasMaxLength(2)
                        .HasColumnType("nvarchar(2)");

                    b.Property<string>("ContinentCode")
                        .IsRequired()
                        .HasMaxLength(2)
                        .HasColumnType("nvarchar(2)");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int>("NumericCode")
                        .HasColumnType("int");

                    b.HasKey("Code");

                    b.HasIndex("ContinentCode");

                    b.HasIndex("CurrencyCode");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Countries");

                    b.HasData(
                        new
                        {
                            Code = "CA",
                            ContinentCode = "NA",
                            CurrencyCode = "CAD",
                            Name = "Canada",
                            NumericCode = 124
                        },
                        new
                        {
                            Code = "CN",
                            ContinentCode = "AS",
                            CurrencyCode = "CNY",
                            Name = "China",
                            NumericCode = 156
                        },
                        new
                        {
                            Code = "FR",
                            ContinentCode = "EU",
                            CurrencyCode = "EUR",
                            Name = "France",
                            NumericCode = 250
                        },
                        new
                        {
                            Code = "DE",
                            ContinentCode = "EU",
                            CurrencyCode = "EUR",
                            Name = "Germany",
                            NumericCode = 276
                        },
                        new
                        {
                            Code = "IN",
                            ContinentCode = "AS",
                            CurrencyCode = "INR",
                            Name = "India",
                            NumericCode = 356
                        },
                        new
                        {
                            Code = "JP",
                            ContinentCode = "AS",
                            CurrencyCode = "JPY",
                            Name = "Japan",
                            NumericCode = 392
                        },
                        new
                        {
                            Code = "RU",
                            ContinentCode = "EU",
                            CurrencyCode = "RUB",
                            Name = "Russia",
                            NumericCode = 643
                        },
                        new
                        {
                            Code = "KR",
                            ContinentCode = "AS",
                            CurrencyCode = "KRW",
                            Name = "South Korea",
                            NumericCode = 410
                        },
                        new
                        {
                            Code = "GB",
                            ContinentCode = "EU",
                            CurrencyCode = "GBP",
                            Name = "United Kingdom",
                            NumericCode = 826
                        },
                        new
                        {
                            Code = "US",
                            ContinentCode = "NA",
                            CurrencyCode = "USD",
                            Name = "United States",
                            NumericCode = 840
                        });
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Currency", b =>
                {
                    b.Property<string>("Code")
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int>("NumericCode")
                        .HasColumnType("int");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasMaxLength(5)
                        .HasColumnType("nvarchar(5)");

                    b.HasKey("Code");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.HasIndex("NumericCode")
                        .IsUnique();

                    b.ToTable("Currencies");

                    b.HasData(
                        new
                        {
                            Code = "USD",
                            Name = "United States dollar",
                            NumericCode = 840,
                            Symbol = "$"
                        },
                        new
                        {
                            Code = "CAD",
                            Name = "Canadian dollar",
                            NumericCode = 124,
                            Symbol = "$"
                        },
                        new
                        {
                            Code = "MXN",
                            Name = "Mexican peso",
                            NumericCode = 484,
                            Symbol = "$"
                        },
                        new
                        {
                            Code = "GBP",
                            Name = "British pound",
                            NumericCode = 826,
                            Symbol = "£"
                        },
                        new
                        {
                            Code = "EUR",
                            Name = "Euro",
                            NumericCode = 978,
                            Symbol = "€"
                        },
                        new
                        {
                            Code = "JPY",
                            Name = "Japanese yen",
                            NumericCode = 392,
                            Symbol = "¥"
                        },
                        new
                        {
                            Code = "CNY",
                            Name = "Chinese yuan",
                            NumericCode = 156,
                            Symbol = "¥"
                        },
                        new
                        {
                            Code = "INR",
                            Name = "Indian rupee",
                            NumericCode = 356,
                            Symbol = "₹"
                        },
                        new
                        {
                            Code = "AUD",
                            Name = "Australian dollar",
                            NumericCode = 36,
                            Symbol = "$"
                        },
                        new
                        {
                            Code = "BRL",
                            Name = "Brazilian real",
                            NumericCode = 986,
                            Symbol = "R$"
                        },
                        new
                        {
                            Code = "RUB",
                            Name = "Russian ruble",
                            NumericCode = 643,
                            Symbol = "₽"
                        },
                        new
                        {
                            Code = "KRW",
                            Name = "South Korean won",
                            NumericCode = 410,
                            Symbol = "₩"
                        });
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Exchange", b =>
                {
                    b.Property<string>("Code")
                        .HasMaxLength(4)
                        .HasColumnType("nvarchar(4)");

                    b.Property<int>("CityId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Code");

                    b.HasIndex("CityId");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Exchanges");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.ExchangeHoliday", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("ExchangeCode")
                        .IsRequired()
                        .HasMaxLength(4)
                        .HasColumnType("nvarchar(4)");

                    b.Property<DateTime>("HolidayDate")
                        .HasColumnType("Date");

                    b.HasKey("_id");

                    b.HasIndex("Description")
                        .IsUnique();

                    b.HasIndex("ExchangeCode", "HolidayDate")
                        .IsUnique();

                    b.ToTable("ExchangeHolidays");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.ExchangeSchedule", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<TimeSpan>("CloseTime")
                        .HasColumnType("time");

                    b.Property<int>("DayOfWeek")
                        .HasColumnType("int");

                    b.Property<string>("ExchangeCode")
                        .IsRequired()
                        .HasMaxLength(4)
                        .HasColumnType("nvarchar(4)");

                    b.Property<TimeSpan>("OpenTime")
                        .HasColumnType("time");

                    b.HasKey("_id");

                    b.HasIndex("ExchangeCode", "DayOfWeek")
                        .IsUnique();

                    b.ToTable("ExchangeSchedules");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.FxRate", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<string>("BaseCurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)");

                    b.Property<string>("QuoteCurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)");

                    b.Property<decimal>("Rate")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTime>("RateDate")
                        .HasColumnType("Date");

                    b.HasKey("_id");

                    b.HasIndex("BaseCurrencyCode");

                    b.HasIndex("QuoteCurrencyCode");

                    b.HasIndex("RateDate", "BaseCurrencyCode", "QuoteCurrencyCode")
                        .IsUnique();

                    b.ToTable("FxRates");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Security", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<int>("AssetClassCode")
                        .HasColumnType("int");

                    b.Property<string>("ExchangeCode")
                        .IsRequired()
                        .HasColumnType("nvarchar(4)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.HasKey("_id");

                    b.HasIndex("AssetClassCode");

                    b.HasIndex("ExchangeCode");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Securities");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.SecurityPrice", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<decimal>("ClosePrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("Dividend")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("HighPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("LowPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("OpenPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("SecurityId")
                        .HasColumnType("int");

                    b.Property<DateTime>("TradeDate")
                        .HasColumnType("Date");

                    b.Property<int>("Volume")
                        .HasColumnType("int");

                    b.HasKey("_id");

                    b.HasIndex("SecurityId");

                    b.HasIndex("TradeDate", "SecurityId")
                        .IsUnique();

                    b.ToTable("SecurityPrices");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.SecuritySymbol", b =>
                {
                    b.Property<int>("_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("Id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("_id"));

                    b.Property<int>("SecurityId")
                        .HasColumnType("int");

                    b.Property<int>("Standard")
                        .HasColumnType("int");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("_id");

                    b.HasIndex("Standard");

                    b.HasIndex("SecurityId", "Standard")
                        .IsUnique();

                    b.ToTable("SecuritySymbols");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.SymbolStandard", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.HasIndex("Description")
                        .IsUnique();

                    b.ToTable("SymbolStandards");

                    b.HasData(
                        new
                        {
                            Id = 0,
                            Description = "CUSIP"
                        },
                        new
                        {
                            Id = 1,
                            Description = "ISIN"
                        },
                        new
                        {
                            Id = 2,
                            Description = "SEDOL"
                        },
                        new
                        {
                            Id = 3,
                            Description = "RIC"
                        },
                        new
                        {
                            Id = 4,
                            Description = "Bloomberg Ticker"
                        });
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.TimeZone", b =>
                {
                    b.Property<string>("Code")
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("TimeZoneOffset")
                        .IsRequired()
                        .HasMaxLength(6)
                        .HasColumnType("nvarchar(6)");

                    b.Property<bool>("UsesDaylightSaving")
                        .HasColumnType("bit");

                    b.HasKey("Code");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("TimeZones");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.City", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Country", null)
                        .WithMany()
                        .HasForeignKey("CountryCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Boutquin.Trading.Domain.Entities.TimeZone", null)
                        .WithMany()
                        .HasForeignKey("TimeZoneCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Country", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Continent", null)
                        .WithMany()
                        .HasForeignKey("ContinentCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Boutquin.Trading.Domain.Entities.Currency", null)
                        .WithMany()
                        .HasForeignKey("CurrencyCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Exchange", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.City", "City")
                        .WithMany()
                        .HasForeignKey("CityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("City");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.ExchangeHoliday", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Exchange", null)
                        .WithMany("ExchangeHolidays")
                        .HasForeignKey("ExchangeCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.ExchangeSchedule", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Exchange", null)
                        .WithMany("ExchangeSchedules")
                        .HasForeignKey("ExchangeCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.FxRate", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Currency", null)
                        .WithMany()
                        .HasForeignKey("BaseCurrencyCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Boutquin.Trading.Domain.Entities.Currency", null)
                        .WithMany()
                        .HasForeignKey("QuoteCurrencyCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Security", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.AssetClass", null)
                        .WithMany()
                        .HasForeignKey("AssetClassCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Boutquin.Trading.Domain.Entities.Exchange", "Exchange")
                        .WithMany()
                        .HasForeignKey("ExchangeCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Exchange");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.SecurityPrice", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Security", null)
                        .WithMany("SecurityPrices")
                        .HasForeignKey("SecurityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.SecuritySymbol", b =>
                {
                    b.HasOne("Boutquin.Trading.Domain.Entities.Security", null)
                        .WithMany("SecuritySymbols")
                        .HasForeignKey("SecurityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Boutquin.Trading.Domain.Entities.SymbolStandard", null)
                        .WithMany()
                        .HasForeignKey("Standard")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Exchange", b =>
                {
                    b.Navigation("ExchangeHolidays");

                    b.Navigation("ExchangeSchedules");
                });

            modelBuilder.Entity("Boutquin.Trading.Domain.Entities.Security", b =>
                {
                    b.Navigation("SecurityPrices");

                    b.Navigation("SecuritySymbols");
                });
#pragma warning restore 612, 618
        }
    }
}
