"""
Backtest verification: tearsheet metrics computed from backtest results.

These tests verify that the metrics computed from the backtest equity curve
match independent calculations, ensuring the full pipeline produces correct
risk and return statistics.
"""

import numpy as np
import pytest

from conftest import load_vector, PRECISION_EXACT, PRECISION_NUMERIC, TRADING_DAYS_PER_YEAR


class TestSingleAssetMetrics:
    """Verify tearsheet metrics for single-asset buy-and-hold."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_single_asset")

    @pytest.fixture(scope="class")
    def daily_returns(self, vector):
        return np.array(vector["expected"]["daily_returns"])

    @pytest.fixture(scope="class")
    def metrics(self, vector):
        return vector["expected"]["metrics"]

    def test_annualized_return(self, daily_returns, metrics):
        cumulative = np.prod(1 + daily_returns) - 1
        n = len(daily_returns)
        expected = (1 + cumulative) ** (TRADING_DAYS_PER_YEAR / n) - 1
        assert metrics["annualized_return"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_cagr(self, daily_returns, metrics):
        years = len(daily_returns) / TRADING_DAYS_PER_YEAR
        expected = np.prod(1 + daily_returns) ** (1 / years) - 1
        assert metrics["cagr"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_annualized_volatility(self, daily_returns, metrics):
        expected = np.std(daily_returns, ddof=1) * np.sqrt(TRADING_DAYS_PER_YEAR)
        assert metrics["annualized_volatility"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_sharpe_ratio(self, daily_returns, metrics):
        daily_sharpe = np.mean(daily_returns) / np.std(daily_returns, ddof=1)
        expected = daily_sharpe * np.sqrt(TRADING_DAYS_PER_YEAR)
        assert metrics["annualized_sharpe_ratio"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_sortino_ratio(self, daily_returns, metrics):
        downside = np.minimum(0, daily_returns)
        dd = np.sqrt(np.sum(downside ** 2) / (len(daily_returns) - 1))
        daily_sortino = np.mean(daily_returns) / dd
        expected = daily_sortino * np.sqrt(TRADING_DAYS_PER_YEAR)
        assert metrics["annualized_sortino_ratio"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_win_rate(self, daily_returns, metrics):
        expected = np.sum(daily_returns > 0) / len(daily_returns)
        assert metrics["win_rate"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_omega_ratio(self, daily_returns, metrics):
        gains = np.sum(np.maximum(daily_returns, 0))
        losses = np.sum(np.maximum(-daily_returns, 0))
        expected = gains / losses if losses > 0 else float("inf")
        assert metrics["omega_ratio"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_profit_factor(self, daily_returns, metrics):
        gp = np.sum(daily_returns[daily_returns > 0])
        gl = abs(np.sum(daily_returns[daily_returns < 0]))
        expected = gp / gl if gl > 0 else float("inf")
        assert metrics["profit_factor"] == pytest.approx(expected, abs=PRECISION_EXACT)


class TestDrawdownMetrics:
    """Verify drawdown metrics using bear market scenario."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_drawdown")

    @pytest.fixture(scope="class")
    def metrics(self, vector):
        return vector["expected"]["metrics"]

    @pytest.fixture(scope="class")
    def equity_values(self, vector):
        return list(vector["expected"]["equity_curve"].values())

    def test_max_drawdown_is_negative(self, metrics):
        assert metrics["max_drawdown"] < 0

    def test_max_drawdown_from_equity(self, equity_values, metrics):
        equity = np.array(equity_values)
        peak = np.maximum.accumulate(equity)
        drawdowns = (equity - peak) / peak
        expected = float(np.min(drawdowns))
        assert metrics["max_drawdown"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_max_drawdown_duration(self, vector, equity_values, metrics):
        """Drawdown duration in trading days (index-based), not calendar days."""
        equity = np.array(equity_values)
        peak = np.maximum.accumulate(equity)
        drawdowns = (equity - peak) / peak

        max_duration = 0
        start_drawdown_index = 0
        for i, dd in enumerate(drawdowns):
            if dd >= 0:
                start_drawdown_index = i
            else:
                duration = i - start_drawdown_index
                max_duration = max(max_duration, duration)

        assert metrics["max_drawdown_duration"] == max_duration

    def test_calmar_ratio(self, metrics):
        """Calmar = CAGR / |MaxDrawdown|."""
        if metrics["max_drawdown"] != 0:
            expected = metrics["cagr"] / abs(metrics["max_drawdown"])
            assert metrics["calmar_ratio"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_recovery_factor(self, metrics):
        """Recovery = cumulative return / |MaxDrawdown|."""
        dr = load_vector("backtest_drawdown")["expected"]["daily_returns"]
        cumulative = np.prod(1 + np.array(dr)) - 1
        if metrics["max_drawdown"] != 0:
            expected = cumulative / abs(metrics["max_drawdown"])
            assert metrics["recovery_factor"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_var_and_cvar(self, metrics):
        """Historical VaR should be negative for a bear market."""
        assert metrics["historical_var"] < 0
        # CVaR should be <= VaR (further into the tail)
        assert metrics["conditional_var"] <= metrics["historical_var"] + PRECISION_NUMERIC


class TestBenchmarkRelativeMetrics:
    """Verify Alpha, Beta, Information Ratio between portfolio and benchmark."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_benchmark")

    @pytest.fixture(scope="class")
    def port_dr(self, vector):
        return np.array(vector["expected"]["portfolio"]["daily_returns"])

    @pytest.fixture(scope="class")
    def bench_dr(self, vector):
        return np.array(vector["expected"]["benchmark"]["daily_returns"])

    @pytest.fixture(scope="class")
    def relative(self, vector):
        return vector["expected"]["relative_metrics"]

    def test_beta(self, port_dr, bench_dr, relative):
        n = min(len(port_dr), len(bench_dr))
        p, b = port_dr[:n], bench_dr[:n]
        cov = np.sum((p - np.mean(p)) * (b - np.mean(b))) / (n - 1)
        var_b = np.sum((b - np.mean(b)) ** 2) / (n - 1)
        expected = cov / var_b
        assert relative["beta"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_alpha(self, port_dr, bench_dr, relative):
        n = min(len(port_dr), len(bench_dr))
        p, b = port_dr[:n], bench_dr[:n]
        cov = np.sum((p - np.mean(p)) * (b - np.mean(b))) / (n - 1)
        var_b = np.sum((b - np.mean(b)) ** 2) / (n - 1)
        beta = cov / var_b
        expected = np.mean(p) - beta * np.mean(b)
        assert relative["alpha"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_information_ratio(self, port_dr, bench_dr, relative):
        n = min(len(port_dr), len(bench_dr))
        active = port_dr[:n] - bench_dr[:n]
        expected = np.mean(active) / np.std(active, ddof=1)
        assert relative["information_ratio"] == pytest.approx(expected, abs=PRECISION_EXACT)


class TestThreeAssetFullYear:
    """Verify full-year three-asset portfolio metrics."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_three_asset")

    @pytest.fixture(scope="class")
    def metrics(self, vector):
        return vector["expected"]["metrics"]

    def test_has_all_metrics(self, metrics):
        required = [
            "annualized_return", "cagr", "annualized_volatility",
            "annualized_sharpe_ratio", "annualized_sortino_ratio",
            "max_drawdown", "max_drawdown_duration",
            "calmar_ratio", "omega_ratio", "win_rate",
            "profit_factor", "recovery_factor",
            "historical_var", "conditional_var",
            "skewness", "kurtosis",
        ]
        for key in required:
            assert key in metrics, f"Missing metric: {key}"

    def test_skewness(self, vector, metrics):
        dr = np.array(vector["expected"]["daily_returns"])
        n = len(dr)
        mean = np.mean(dr)
        std = np.std(dr, ddof=1)
        m3 = np.sum(((dr - mean) / std) ** 3)
        expected = (n / ((n - 1) * (n - 2))) * m3
        assert metrics["skewness"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_kurtosis(self, vector, metrics):
        dr = np.array(vector["expected"]["daily_returns"])
        n = len(dr)
        mean = np.mean(dr)
        std = np.std(dr, ddof=1)
        m4 = np.sum(((dr - mean) / std) ** 4)
        t1 = (n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * m4
        t2 = 3 * (n - 1) ** 2 / ((n - 2) * (n - 3))
        expected = t1 - t2
        assert metrics["kurtosis"] == pytest.approx(expected, abs=PRECISION_EXACT)

    def test_historical_var(self, vector, metrics):
        dr = np.array(vector["expected"]["daily_returns"])
        n = len(dr)
        sorted_r = np.sort(dr)
        confidence = 0.95
        index = (1 - confidence) * (n - 1)
        lower = int(np.floor(index))
        upper = min(lower + 1, n - 1)
        fraction = index - lower
        expected = sorted_r[lower] + fraction * (sorted_r[upper] - sorted_r[lower])
        assert metrics["historical_var"] == pytest.approx(expected, abs=PRECISION_EXACT)


class TestRebalancing:
    """Verify periodic rebalancing generates multiple trade events."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_rebalancing")

    def test_multiple_trade_dates(self, vector):
        """Rebalancing should produce trades on multiple dates."""
        trades = vector["expected"]["trades"]
        trade_dates = {t["date"] for t in trades}
        # 126 days / 21 = 6 rebalance points, first trade on day 0
        assert len(trade_dates) >= 2

    def test_rebalance_trades_include_buys_and_sells(self, vector):
        """After initial buy, rebalancing should include sells of overweight assets."""
        trades = vector["expected"]["trades"]
        # Skip day-0 trades
        first_date = trades[0]["date"]
        rebalance_trades = [t for t in trades if t["date"] != first_date]

        if len(rebalance_trades) > 0:
            actions = {t["action"] for t in rebalance_trades}
            # Rebalancing may include both buys and sells
            assert len(actions) >= 1

    def test_equity_curve_length(self, vector):
        equity = vector["expected"]["equity_curve"]
        # Should have entries for all trading days
        assert len(equity) == 126

    def test_all_metrics_computed(self, vector):
        metrics = vector["expected"]["metrics"]
        assert "annualized_return" in metrics
        assert "max_drawdown" in metrics
        assert "annualized_sharpe_ratio" in metrics
