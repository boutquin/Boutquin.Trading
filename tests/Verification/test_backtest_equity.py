"""
Backtest verification: equity curve, positions, and cash tracking.

Tests validate the reference Python backtest engine's outputs against
the golden vector files, ensuring self-consistency before C# cross-checks.

Convention: Next-bar Open fills. Signals generated on bar T fill at bar T+1's Open.
"""

import numpy as np
import pytest

from conftest import load_vector, PRECISION_EXACT, PRECISION_NUMERIC


class TestSingleAssetBuyAndHold:
    """Scenario 1: Single-asset buy-and-hold, no commission."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_single_asset")

    def test_equity_curve_length(self, vector):
        market_data = vector["inputs"]["market_data"]["AAPL"]
        equity = vector["expected"]["equity_curve"]
        assert len(equity) == len(market_data)

    def test_day_zero_equity_equals_initial_cash(self, vector):
        """Day 0: no fills yet (orders are pending), equity = initial cash."""
        equity = vector["expected"]["equity_curve"]
        initial_cash = vector["inputs"]["initial_cash"]
        first_equity = list(equity.values())[0]
        assert first_equity == pytest.approx(initial_cash, abs=PRECISION_EXACT)

    def test_position_filled_on_day_one(self, vector):
        """Next-bar fill: signals on day 0, fills at day 1's Open."""
        trades = vector["expected"]["trades"]
        assert len(trades) >= 1
        assert trades[0]["action"] == "Buy"
        assert trades[0]["quantity"] > 0
        # Fill date is day 1 (second trading day), not day 0
        dates = sorted(vector["expected"]["equity_curve"].keys())
        assert trades[0]["date"] == dates[1]

    def test_fill_price_is_open(self, vector):
        """Market orders fill at next bar's Open, not AdjustedClose."""
        trades = vector["expected"]["trades"]
        fill_date = trades[0]["date"]
        market_data = vector["inputs"]["market_data"]["AAPL"]
        day_data = next(r for r in market_data if r["date"] == fill_date)
        assert trades[0]["fill_price"] == pytest.approx(day_data["open"], abs=PRECISION_EXACT)

    def test_no_subsequent_trades(self, vector):
        """Buy-and-hold: only trades on fill date (day 1)."""
        trades = vector["expected"]["trades"]
        dates = {t["date"] for t in trades}
        assert len(dates) == 1

    def test_equity_tracks_price(self, vector):
        """Equity should equal position * AdjustedClose + cash."""
        market_data = vector["inputs"]["market_data"]["AAPL"]
        equity = vector["expected"]["equity_curve"]
        equity_vals = list(equity.values())

        positions = vector["expected"]["positions"]
        dates = list(positions.keys())
        for i in range(len(equity_vals)):
            pos = positions[dates[i]]["AAPL"]
            cash = vector["expected"]["cash"][dates[i]]
            expected_equity = pos * market_data[i]["adjusted_close"] + cash
            assert equity_vals[i] == pytest.approx(expected_equity, abs=PRECISION_EXACT)

    def test_daily_returns_from_equity(self, vector):
        """Daily returns should match (E[t] - E[t-1]) / E[t-1]."""
        equity_vals = list(vector["expected"]["equity_curve"].values())
        daily_returns = vector["expected"]["daily_returns"]

        assert len(daily_returns) == len(equity_vals) - 1
        for i in range(len(daily_returns)):
            expected = (equity_vals[i + 1] - equity_vals[i]) / equity_vals[i]
            assert daily_returns[i] == pytest.approx(expected, abs=PRECISION_EXACT)


class TestSingleAssetWithCommission:
    """Scenario 2: Commission reduces cash available."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_single_asset_commission")

    @pytest.fixture(scope="class")
    def no_comm_vector(self):
        return load_vector("backtest_single_asset")

    def test_commission_recorded_in_trade(self, vector):
        trades = vector["expected"]["trades"]
        assert len(trades) >= 1
        assert trades[0]["commission"] > 0

    def test_commission_formula(self, vector):
        """Commission = fillPrice * quantity * commissionRate."""
        trade = vector["expected"]["trades"][0]
        rate = vector["inputs"]["commission_rate"]
        expected_comm = trade["fill_price"] * trade["quantity"] * rate
        assert trade["commission"] == pytest.approx(expected_comm, abs=PRECISION_EXACT)

    def test_cash_after_buy_deducts_commission(self, vector):
        """Cash on fill date = initial - (price * qty + commission)."""
        trade = vector["expected"]["trades"][0]
        initial = vector["inputs"]["initial_cash"]
        cost = trade["fill_price"] * trade["quantity"] + trade["commission"]
        fill_date = trade["date"]
        actual_cash = vector["expected"]["cash"][fill_date]
        assert actual_cash == pytest.approx(initial - cost, abs=PRECISION_EXACT)


class TestMultiAssetFixedWeights:
    """Scenario 3: Multi-asset position sizing."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_multi_asset")

    def test_two_assets_traded(self, vector):
        trades = vector["expected"]["trades"]
        tickers = {t["ticker"] for t in trades}
        assert tickers == {"AAPL", "MSFT"}

    def test_position_sizing_respects_weights(self, vector):
        """Position value should be approximately weight * totalValue."""
        trades = vector["expected"]["trades"]
        initial_cash = vector["inputs"]["initial_cash"]
        weights = vector["inputs"]["weights"]

        for trade in trades:
            ticker = trade["ticker"]
            notional = trade["fill_price"] * trade["quantity"]
            expected_notional = initial_cash * weights[ticker]
            # Allow 1 share rounding tolerance + Open vs AdjustedClose price difference
            tolerance = trade["fill_price"] * 2.0
            assert abs(notional - expected_notional) < tolerance

    def test_equity_curve_has_correct_length(self, vector):
        # Both assets have 60 days of data
        equity = vector["expected"]["equity_curve"]
        assert len(equity) == 60


class TestCommissionImpact:
    """Scenario 4: Commission impact comparison."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_commission_impact")

    def test_no_commission_higher_equity(self, vector):
        """Portfolio without commission should have higher or equal equity."""
        no_comm = vector["expected"]["no_commission"]["equity_curve"]
        with_comm = vector["expected"]["with_commission"]["equity_curve"]

        dates = sorted(set(no_comm.keys()) & set(with_comm.keys()))
        for date in dates:
            assert no_comm[date] >= with_comm[date] - PRECISION_NUMERIC

    def test_commission_reduces_position_or_cash(self, vector):
        """Commission should reduce either position size or cash."""
        no_comm_trades = vector["expected"]["no_commission"]["trades"]
        with_comm_trades = vector["expected"]["with_commission"]["trades"]

        no_comm_qty = sum(t["quantity"] for t in no_comm_trades)
        with_comm_qty = sum(t["quantity"] for t in with_comm_trades)

        # With next-bar fills, compare cash on fill date
        if no_comm_trades and with_comm_trades:
            no_comm_cash = list(vector["expected"]["no_commission"]["cash"].values())
            with_comm_cash = list(vector["expected"]["with_commission"]["cash"].values())
            # Either fewer shares or less cash after fill
            assert with_comm_qty <= no_comm_qty or with_comm_cash[1] < no_comm_cash[1]


class TestPositionSizingRounding:
    """Scenario 7: AwayFromZero rounding."""

    @pytest.fixture(scope="class")
    def vector(self):
        return load_vector("backtest_rounding")

    def test_rounding_method(self, vector):
        """Position should use AwayFromZero rounding."""
        detail = vector["expected"]["rounding_detail"]
        raw = detail["raw_value"]
        rounded = detail["rounded"]

        # Python's round() uses banker's rounding; our function uses AwayFromZero
        import math
        expected = int(math.floor(raw + 0.5)) if raw >= 0 else int(math.ceil(raw - 0.5))
        assert rounded == expected

    def test_initial_position_matches(self, vector):
        """First trade quantity should match expected rounding."""
        trades = vector["expected"]["trades"]
        expected_pos = vector["expected"]["expected_initial_position"]
        assert trades[0]["quantity"] == expected_pos
