// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using System.Globalization;
using System.Text;

using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.SlippageModels;
using Boutquin.Trading.Domain.Helpers;
using Boutquin.Trading.Recipes.Testing;

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// AC-4 hot-path parity characterization test: freezes the pre-Money equity-curve and
/// tearsheet metrics as a deterministic golden captured at pre-Money HEAD (commit cc62330).
///
/// Purpose: prove that the M-9 Money adoption (boundary-only) did NOT perturb the
/// per-bar decimal hot-loop inside Backtest.RunAsync. If any Money value escapes into
/// the hot path the equity-curve or any metric will change, turning this test RED.
///
/// Fixture: a single-asset BuyAndHold backtest over 252 synthetic trading days, seeded
/// at 42, identical to the BuyAndHoldBenchmark.Run fixture in benchmarks/BenchMark/ --
/// the same deterministic dataset (GBM, SPY $470, 16% vol, 10% drift, seed 42) and the
/// same commission rate (10 bps). Self-contained in the UnitTests project: no reference
/// to the benchmark project is added.
///
/// Golden values were captured by running the backtest once at pre-Money HEAD and
/// embedding the canonical string below. Any per-bar perturbation will produce a
/// different canonical string and the test will fail.
/// </summary>
public sealed class HotPathParityCharacterizationTests
{
    // -------------------------------------------------------------------------
    // Frozen golden captured at pre-Money HEAD (commit cc62330, 2026-06-22).
    // DO NOT edit unless you are deliberately rebasing the characterization
    // (e.g., a non-Money infrastructure change that legitimately alters the
    // equity curve).  A change here without a spec-cited justification is a
    // red flag that Money leaked into the hot path.
    //
    // Quick-check: the equity curve should have 252 entries, starting on
    // 2024-01-02 at 100000.0 and ending on 2025-01-08.
    // -------------------------------------------------------------------------
    // Golden captured at pre-Money HEAD (commit cc62330, 2026-06-22).
    // Equity curve: 252 entries, 2024-01-02 to 2024-12-18, starting at 100000.
    // Full tearsheet metrics captured with G29 InvariantCulture formatting.
    // Rebased 2026-09-26 for Boutquin.Numerics a0addcf: Skewness changes in its 28th
    // significant digit (…861546 → …861543), from Numerics' moment-accumulation fixes. The
    // equity curve and every other metric are byte-identical, and the old Numerics build
    // (53bf909) still reproduces the prior value, so the delta is upstream, not a hot-path leak.
    private const string GoldenCanonicalOutput =
        "EC:2024-01-02|100000;2024-01-03|100781.10296340930491720711683;2024-01-04|100551.66048701328718461978314;2024-01-05|101054.10625996763395141811931;2024-01-08|100925.77244327314793692442977;2024-01-09|99847.10367649050804421739771;2024-01-10|100640.83653469807194022307733;2024-01-11|100523.879585802147268021833;2024-01-12|100937.47441677326367032622856;2024-01-15|100910.27618333931582343890068;2024-01-16|101919.60927325297968969827794;2024-01-17|103020.25487386005966643580161;2024-01-18|103199.93423337531025834835394;2024-01-19|104162.95660190585169960191264;2024-01-22|105780.62787196692685544236013;2024-01-23|106244.84247591333451322961265;2024-01-24|105665.06126377837270262799616;2024-01-25|105594.06380630395987208720031;2024-01-26|105877.60667114641746122251922;2024-01-29|106232.85073291590832936949397;2024-01-30|106748.5769437844528241251959;2024-01-31|107014.91974088444112722042467;2024-02-01|106983.39967213559501173678831;2024-02-02|107557.74504695558146674926625;2024-02-05|109611.53124923488097736452601;2024-02-06|108141.44935449653945517150064;2024-02-07|109530.35951152061683393527165;2024-02-08|109909.38620489087361090095518;2024-02-09|110088.80499681570525143542467;2024-02-12|109297.28813260220994649386185;2024-02-13|108915.02811925498845870299708;2024-02-14|106957.12817716218359391387098;2024-02-15|106920.9658250073319536982755;2024-02-16|107400.74166311154406680866495;2024-02-19|107773.58574312724235424885526;2024-02-20|106828.04448524269882654235251;2024-02-21|105939.43300596372529092784428;2024-02-22|104851.8870372561485034872008;2024-02-23|103431.83522531822071436498769;2024-02-26|102436.39939128649977233703247;2024-02-27|105116.72977190132133272319082;2024-02-28|106568.94262975827547030730336;2024-02-29|105652.62398986572686419069408;2024-03-01|106887.45038303669612777928827;2024-03-04|106941.92960985300241761092918;2024-03-05|105845.95454177374741551719225;2024-03-06|105458.48278384824251589197005;2024-03-07|105889.069058401704858496521;2024-03-08|106172.69081323032340342094175;2024-03-11|107492.64867929649119737975869;2024-03-12|109128.43026557879720593044088;2024-03-13|107207.29631873146577247912346;2024-03-14|106544.00791963431296127416648;2024-03-15|104887.71126826235369211864342;2024-03-18|105178.69332883547366224186677;2024-03-19|104055.75423999422656949748673;2024-03-20|104126.30135162983596364596043;2024-03-21|105788.88215068531798984247521;2024-03-22|106145.36653586746557002971747;2024-03-25|106594.53251566365523032371705;2024-03-26|105845.27979754514757524153074;2024-03-27|104554.79185462944750932494081;2024-03-28|103751.07819787268663354660739;2024-03-29|104834.83137419821741241471935;2024-04-01|105524.32783730866404380090247;2024-04-02|105676.50701086418247582098261;2024-04-03|104173.66775826126249823692495;2024-04-04|104934.60174998963666072516083;2024-04-05|104719.82514755911562399430697;2024-04-08|106442.56872502657479965748475;2024-04-09|106132.89747988428702027253434;2024-04-10|107430.93720845286392717136946;2024-04-11|104942.20521002243961377962222;2024-04-12|105476.77856136493045095964986;2024-04-15|104702.11406856815192638886501;2024-04-16|104644.91971390681314453990968;2024-04-17|104433.44934532573856653468086;2024-04-18|103965.13725918451378317825265;2024-04-19|104248.42849247414257565356729;2024-04-22|103914.43697848691442218171349;2024-04-23|103037.36961806885653242447361;2024-04-24|103951.22201228126025487964143;2024-04-25|105095.52124670839633983880314;2024-04-26|104676.37667368208157723208192;2024-04-29|103433.62199171468378498536484;2024-04-30|102231.14871317316977042590372;2024-05-01|103266.20290056797703582190283;2024-05-02|102799.49490251577990212556931;2024-05-03|103147.53604446171213569909902;2024-05-06|103668.38531652436789549034003;2024-05-07|103858.85102082516326129216623;2024-05-08|101768.81832442070864609015764;2024-05-09|102875.47826635495599061984415;2024-05-10|103234.88286487611169864494825;2024-05-13|104994.13177122702248266197373;2024-05-14|104559.80726024765827364071876;2024-05-15|103303.94202569504391568848717;2024-05-16|102902.7282785727842300990235;2024-05-17|103450.75352834021814520883742;2024-05-20|103956.46846762807567377740723;2024-05-21|104544.43572868849900785852118;2024-05-22|104182.09693129246030233837601;2024-05-23|104113.82928971385279741895914;2024-05-24|104891.8531159367165112348582;2024-05-27|105424.62429440031625179789154;2024-05-28|105701.7053906883337884879939;2024-05-29|104526.15916700186703855314016;2024-05-30|104230.22385412652956363987792;2024-05-31|104253.92351166242377108226392;2024-06-03|104050.06460501402724432280643;2024-06-04|103711.13100882888215021066869;2024-06-05|104219.8697394304328594223;2024-06-06|105815.08074686561369768722265;2024-06-07|105254.07903365195109902894669;2024-06-10|104674.80942427249884901779552;2024-06-11|102959.58695757940947693662454;2024-06-12|102204.32822649308969636338073;2024-06-13|102270.29058705860876669143038;2024-06-14|101067.78156511471508642631985;2024-06-17|100050.06342960722271132918189;2024-06-18|101314.35222162733744056872604;2024-06-19|101298.30220004631750012004108;2024-06-20|101188.87174546738365708534913;2024-06-21|101209.54791012102521890201104;2024-06-24|101166.87727694112653356168422;2024-06-25|98190.18129477207053531832796;2024-06-26|98569.93957486785669859626885;2024-06-27|98890.45071373764513791343977;2024-06-28|99079.49449433413784122603752;2024-07-01|100595.21230803742482838679226;2024-07-02|100852.24162747693748810670365;2024-07-03|101529.4355505934645431740662;2024-07-04|99739.8891247389944349947154;2024-07-05|99963.9672971879720684529329;2024-07-08|99208.17421130716049282061187;2024-07-09|102015.35701372971217365515794;2024-07-10|102332.71834724870080544620341;2024-07-11|101913.73732882454632911652718;2024-07-12|101772.98532501768535210921002;2024-07-15|103746.92410103077456622628645;2024-07-16|103945.06171900452071665205159;2024-07-17|104081.85100814037083534861872;2024-07-18|104639.17603158160854200622287;2024-07-19|104497.80478798369470223187168;2024-07-22|101937.42425114996470761615556;2024-07-23|104670.69387722386530956700835;2024-07-24|103806.30397961591076760912853;2024-07-25|103164.43545402840531878907954;2024-07-26|103230.39531897396274876916631;2024-07-29|103602.12446773293128681547978;2024-07-30|103411.51172296940025623956548;2024-07-31|103817.90809086872877281602556;2024-08-01|104821.31086896269852742454202;2024-08-02|103938.01627038056658507280621;2024-08-05|106257.99567558002251557861923;2024-08-06|107706.28025527342364567373031;2024-08-07|107127.61996056289006032174191;2024-08-08|107294.02632476799583117903527;2024-08-09|106927.56149816052526276174299;2024-08-12|106573.3735139297430872941961;2024-08-13|106231.07600232308784291859911;2024-08-14|107037.5364875986204630479418;2024-08-15|106769.68033258268540640287417;2024-08-16|106360.09887806859993658607042;2024-08-19|106133.90961417028115373149991;2024-08-20|106056.77944513559473307328793;2024-08-21|108388.87976163835712802894813;2024-08-22|108358.0570951404154135705766;2024-08-23|107375.48028517197428516845033;2024-08-26|107020.69550143409067772441759;2024-08-27|105807.85611609345283330602214;2024-08-28|106208.76681876896460547348761;2024-08-29|107014.79497844145167469923763;2024-08-30|108259.57387950566170767910383;2024-09-02|107872.39584571677195256739969;2024-09-03|108931.02609814078326162443609;2024-09-04|108361.80258416062706757451977;2024-09-05|106680.49599621604640964956571;2024-09-06|106285.36723838719092056954147;2024-09-09|107671.86403632342127585986565;2024-09-10|105010.40075596786678253555027;2024-09-11|104629.94033483320213034528771;2024-09-12|103875.16564154279289491726525;2024-09-13|104221.76549145988027017468646;2024-09-16|104274.78003930462800006284775;2024-09-17|104336.46072952522095329638427;2024-09-18|103314.09221818486796478632649;2024-09-19|103512.05516587482517166556451;2024-09-20|102709.10879424479493526972729;2024-09-23|102540.88532063297491214449074;2024-09-24|102574.87173217741948113488615;2024-09-25|102004.73596464626672111348167;2024-09-26|100298.59488772731813838107667;2024-09-27|100792.02515533128985601973259;2024-09-30|100069.53311636666821956164941;2024-10-01|99994.02979698584536152704815;2024-10-02|99844.53102107584461041851296;2024-10-03|98304.47715266792984593525723;2024-10-04|96850.69189204012722018203452;2024-10-07|96771.98751082685066321235332;2024-10-08|96503.92292972009494871189845;2024-10-09|95091.84433276872376328131073;2024-10-10|93028.52347710710848029632291;2024-10-11|94749.82534894671328594092715;2024-10-14|94894.60242669215371717590613;2024-10-15|92665.95472868423456432766105;2024-10-16|94175.1149145383469556514533;2024-10-17|95894.24774938889992739103084;2024-10-18|96192.02251949287801880029773;2024-10-21|96309.03538432818003281654628;2024-10-22|95548.70940363201462577254462;2024-10-23|94813.01878214887805144348917;2024-10-24|94542.15704276409207768486455;2024-10-25|96894.46075964119768492994549;2024-10-28|98099.61614123093645487072498;2024-10-29|99789.36277958531079220016504;2024-10-30|99931.80872516807006003930259;2024-10-31|101717.17340441346999266422792;2024-11-01|102092.78205195615151703383636;2024-11-04|102023.6551769911427149704313;2024-11-05|101611.70010844650537483352586;2024-11-06|102116.43758924803907329690693;2024-11-07|101877.38600449297251999739345;2024-11-08|101416.03633474882501941266884;2024-11-11|100952.17368120585592058457295;2024-11-12|100946.9445879843469389326659;2024-11-13|99713.74128341451407751871581;2024-11-14|99625.90370754908150702964348;2024-11-15|99560.87421964057734964721499;2024-11-18|100899.65343303549638611052188;2024-11-19|102062.90859128903041135518524;2024-11-20|102372.11956665280307281345372;2024-11-21|102160.8527335193851509051921;2024-11-22|102619.72100392673451596357337;2024-11-25|102702.80239550087133327520351;2024-11-26|102311.34948682706316860915157;2024-11-27|101575.09461799173040255798038;2024-11-28|102556.45654762292813966875815;2024-11-29|100932.24445390767547605876281;2024-12-02|100294.42503070666518385562765;2024-12-03|100592.59634019381593506460164;2024-12-04|100420.4377178099853084826042;2024-12-05|101181.45325896883307427474171;2024-12-06|101011.29782563730722835227722;2024-12-09|101067.69702664459407179844447;2024-12-10|101614.05772289833105406708808;2024-12-11|102091.89647513396957149832964;2024-12-12|101745.05609075466489928489085;2024-12-13|100961.22071517360587062416903;2024-12-16|102476.16600672929424637351441;2024-12-17|102709.31863697877526303508927;2024-12-18|104029.76318321528119087750541||TS:AnnualizedReturn=0.04046138531437;SharpeRatio=0.332930318893694326086550211;SortinoRatio=0.4837297968656697834816656703;MaxDrawdown=-0.1582617802839754945379278795;CAGR=0.04046138531437;Volatility=0.1551279552048256087891761372;Alpha=-3.8455347039916407079689E-06;Beta=1.0006045197546212351211480715;InformationRatio=-0.0590209232409022793118898291;CalmarRatio=0.2556611283012771918559249109;OmegaRatio=1.0570524127198627253885388149;HistoricalVaR=-0.0156914530089351063777124822;ConditionalVaR=-0.0207133224095990411322518922;Skewness=0.0545156881997372783875861543;Kurtosis=0.5119127100694966407872383467;WinRate=0.4940239043824701195219123506;ProfitFactor=1.0570524127198627253885388149;RecoveryFactor=0.2546264281865472891943595533;MaxDrawdownDuration=223";

    // -------------------------------------------------------------------------
    // Dataset parameters -- must match BuyAndHoldBenchmark.Run exactly
    // -------------------------------------------------------------------------
    private static readonly Symbol s_spy = new("SPY");
    private static readonly DateOnly s_startDate = new(2024, 1, 2);
    private const int TradingDays = 252;
    private static readonly decimal[] s_startPrices = [470m];
    private static readonly double[] s_annualVols = [0.16];
    private static readonly double[] s_annualDrifts = [0.10];
    private const int Seed = 42;
    private const decimal CommissionRate = 0.001m; // 10 bps

    /// <summary>
    /// The entire deterministic equity curve and every other scalar remain exact; skewness and kurtosis retain tight decimal arithmetic accuracy.
    /// </summary>
    [Fact]
    public async Task FullBacktest_EquityCurveExactAndMomentsWithinDecimalRounding()
    {
        var dataset = BuildDataset();
        var backtest = BuildBacktest(dataset);
        var tearsheet = await backtest.RunAsync(dataset, ignoreDataQualityIssues: true);
        var actual = SerializeBacktestOutput(tearsheet);

        var actualParts = actual.Split("||TS:");
        var expectedParts = GoldenCanonicalOutput.Split("||TS:");
        actualParts[0].Should().Be(expectedParts[0], "the entire equity curve must remain unchanged");
        var expectedMetrics = expectedParts[1].Split(';').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
        foreach (var entry in actualParts[1].Split(';').Select(x => x.Split('=')))
        {
            if (entry[0] is "Skewness" or "Kurtosis")
            {
                decimal.Parse(entry[1], CultureInfo.InvariantCulture).Should().BeApproximately(
                    decimal.Parse(expectedMetrics[entry[0]], CultureInfo.InvariantCulture), 1e-23m,
                    "these standardized moments may change in final decimal rounding after scaled arithmetic");
            }
            else
            {
                entry[1].Should().Be(expectedMetrics[entry[0]], "every other tearsheet scalar is unchanged");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the same deterministic GBM dataset as BuyAndHoldBenchmark.Run.
    /// Self-contained: no reference to the benchmark project.
    /// </summary>
    private static FakeBacktestDataset BuildDataset()
    {
        var rng = new Random(Seed);
        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        decimal[] current = s_startPrices.ToArray();
        var date = s_startDate;

        for (var day = 0; day < TradingDays; day++)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }

            var dayBars = new SortedDictionary<Symbol, Bar>();
            double drift = s_annualDrifts[0] / 252.0;
            double sigma = s_annualVols[0] / Math.Sqrt(252);
            var ret = (decimal)(drift + sigma * NextGaussian(rng));
            var close = Math.Max(0.01m, current[0] * (1m + ret));
            var open = Math.Max(0.01m, current[0] * (1m + (decimal)(sigma * NextGaussian(rng) * 0.3)));
            var high = Math.Max(open, close) * (1m + Math.Abs((decimal)(sigma * NextGaussian(rng) * 0.2)));
            var low = Math.Min(open, close) * Math.Max(0.001m, 1m - Math.Abs((decimal)(sigma * NextGaussian(rng) * 0.2)));
            dayBars[s_spy] = new Bar(date, open, high, low, close, close, 5_000_000L);
            current[0] = close;

            prices[date] = dayBars;
            date = date.AddDays(1);
        }

        return new FakeBacktestDataset { Prices = prices };
    }

    private static BackTest BuildBacktest(FakeBacktestDataset dataset)
    {
        const Currency usd = Currency.USD;
        var assetCurrencies = new Dictionary<Symbol, Currency> { { s_spy, usd } };
        var initialCash = new SortedDictionary<Currency, decimal> { { usd, 100_000m } };

        var costModel = new PercentageOfValueCostModel(CommissionRate);
        var broker = new SimulatedBrokerage(costModel, new NoSlippage());
        var sizer = new FixedWeightPositionSizer(
                            new ReadOnlyDictionary<Symbol, decimal>(
                                new Dictionary<Symbol, decimal> { { s_spy, 1m } }),
                            usd);
        var orderCalc = new ClosePriceOrderPriceCalculationStrategy();

        var strategy = new BuyAndHoldStrategy(
            name: "BuyAndHold-SPY",
            assets: new ReadOnlyDictionary<Symbol, Currency>(assetCurrencies),
            cash: initialCash,
            initialTimestamp: dataset.Prices.Keys.First(),
            orderPriceCalculationStrategy: orderCalc,
            positionSizer: sizer);

        var benchmarkStrategy = new BuyAndHoldStrategy(
            name: "Benchmark-SPY",
            assets: new ReadOnlyDictionary<Symbol, Currency>(assetCurrencies),
            cash: new SortedDictionary<Currency, decimal>(initialCash),
            initialTimestamp: dataset.Prices.Keys.First(),
            orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
            positionSizer: new FixedWeightPositionSizer(
                new ReadOnlyDictionary<Symbol, decimal>(
                    new Dictionary<Symbol, decimal> { { s_spy, 1m } }), usd));

        var handlers = new Dictionary<Type, IEventHandler>
        {
            { typeof(OrderEvent),  new OrderEventHandler() },
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(FillEvent),   new FillEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() },
        };

        var portfolio = new Portfolio(
            usd,
            new ReadOnlyDictionary<string, IStrategy>(
                new Dictionary<string, IStrategy> { { strategy.Name, strategy } }),
            new ReadOnlyDictionary<Symbol, Currency>(assetCurrencies),
            new ReadOnlyDictionary<Type, IEventHandler>(handlers),
            broker);

        var benchmarkPortfolio = new Portfolio(
            usd,
            new ReadOnlyDictionary<string, IStrategy>(
                new Dictionary<string, IStrategy> { { benchmarkStrategy.Name, benchmarkStrategy } }),
            new ReadOnlyDictionary<Symbol, Currency>(assetCurrencies),
            new ReadOnlyDictionary<Type, IEventHandler>(new Dictionary<Type, IEventHandler>(handlers)),
            new SimulatedBrokerage(new PercentageOfValueCostModel(0.00001m), new NoSlippage()));

        return new BackTest(portfolio, benchmarkPortfolio, usd);
    }

    /// <summary>
    /// Serializes the full backtest output (all equity-curve entries + all tearsheet
    /// scalars) to a canonical, culture-invariant string.
    ///
    /// Format: "EC:date1|val1;date2|val2;...||TS:metric1=value1;metric2=value2;..."
    ///
    /// Decimal values use G29 format (full precision, no trailing zeros) in InvariantCulture.
    /// </summary>
    private static string SerializeBacktestOutput(Tearsheet ts)
    {
        var sb = new StringBuilder();

        // --- Equity curve ---
        sb.Append("EC:");
        var first = true;
        foreach (var (ecDate, ecValue) in ts.EquityCurve)
        {
            if (!first)
            {
                sb.Append(';');
            }

            first = false;
            sb.Append(ecDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(ecValue.ToString("G29", CultureInfo.InvariantCulture));
        }

        sb.Append("||TS:");

        // --- Tearsheet scalars (all named fields, in record declaration order) ---
        (string Name, decimal Value)[] metrics =
        [
            ("AnnualizedReturn", ts.AnnualizedReturn),
            ("SharpeRatio",      ts.SharpeRatio),
            ("SortinoRatio",     ts.SortinoRatio),
            ("MaxDrawdown",      ts.MaxDrawdown),
            ("CAGR",             ts.CAGR),
            ("Volatility",       ts.Volatility),
            ("Alpha",            ts.Alpha),
            ("Beta",             ts.Beta),
            ("InformationRatio", ts.InformationRatio),
            ("CalmarRatio",      ts.CalmarRatio),
            ("OmegaRatio",       ts.OmegaRatio),
            ("HistoricalVaR",    ts.HistoricalVaR),
            ("ConditionalVaR",   ts.ConditionalVaR),
            ("Skewness",         ts.Skewness),
            ("Kurtosis",         ts.Kurtosis),
            ("WinRate",          ts.WinRate),
            ("ProfitFactor",     ts.ProfitFactor),
            ("RecoveryFactor",   ts.RecoveryFactor),
        ];

        var firstMetric = true;
        foreach (var (name, val) in metrics)
        {
            if (!firstMetric)
            {
                sb.Append(';');
            }

            firstMetric = false;
            sb.Append(name);
            sb.Append('=');
            sb.Append(val.ToString("G29", CultureInfo.InvariantCulture));
        }

        sb.Append(';');
        sb.Append("MaxDrawdownDuration=");
        sb.Append(ts.MaxDrawdownDuration.ToString(CultureInfo.InvariantCulture));

        return sb.ToString();
    }

    private static double NextGaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
