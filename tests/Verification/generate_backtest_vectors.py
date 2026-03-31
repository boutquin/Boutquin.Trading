#!/usr/bin/env python3
"""
Generate golden test vectors for Boutquin.Trading backtest engine verification.

Produces JSON files in vectors/ that are consumed by both:
  - Python pytest tests (self-consistency check)
  - C# xUnit tests (cross-language verification)

Strategy: Rather than depending on an external backtest framework (zipline, backtrader)
whose fill/commission conventions may diverge, we build a minimal reference backtest
engine in Python that follows the EXACT same conventions as the C# engine:
  - Next-bar Open fills: signals on bar T generate orders filled at bar T+1's Open
  - Commission = fillPrice * quantity * commissionRate
  - Position sizing: round(totalValue * weight / open, MidpointRounding.AwayFromZero)
  - Equity = sum(position * adjustedClose) + cash
  - Daily returns from equity curve: (E[t] - E[t-1]) / E[t-1]
  - Quantity-limiting: buy fills reduced to max affordable qty = floor(cash / (fillPrice + commissionPerShare))
  - Drawdown duration: trading days (not calendar days)

This is the same approach as the calculation vectors: independent Python implementation
using numpy, validated against known manual calculations.

Each vector file contains:
  - inputs: market data, strategy config, cost model config
  - expected: equity curve, trades, positions, cash, metrics
  - library: name + version
  - notes: convention details
"""

import json
import math
import sys
from pathlib import Path
from dataclasses import dataclass, field

import numpy as np
import scipy.stats

VECTORS_DIR = Path(__file__).parent / "vectors"
VECTORS_DIR.mkdir(exist_ok=True)

TRADING_DAYS = 252
RNG = np.random.default_rng(seed=42)


def save(name: str, data: dict) -> None:
    def convert(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, (np.float64, np.float32)):
            v = float(obj)
            # JSON does not support Infinity or NaN — use null
            if v != v or v == float("inf") or v == float("-inf"):
                return None
            return v
        if isinstance(obj, (np.int64, np.int32)):
            return int(obj)
        if isinstance(obj, np.bool_):
            return bool(obj)
        raise TypeError(f"Cannot serialize {type(obj)}")

    def sanitize(obj):
        """Recursively replace inf/nan with None for JSON compatibility."""
        if isinstance(obj, dict):
            return {k: sanitize(v) for k, v in obj.items()}
        if isinstance(obj, list):
            return [sanitize(v) for v in obj]
        if isinstance(obj, float) and (obj != obj or obj == float("inf") or obj == float("-inf")):
            return None
        return obj

    with open(VECTORS_DIR / f"{name}.json", "w") as f:
        json.dump(sanitize(data), f, indent=2, default=convert)
    print(f"  ✓ {name}.json")


# ─── Synthetic Market Data Generation ────────────────────────────────────

def generate_ohlcv(ticker: str, n_days: int, initial_price: float,
                   daily_drift: float = 0.0003, daily_vol: float = 0.015,
                   seed_offset: int = 0) -> list[dict]:
    """Generate synthetic OHLCV data with realistic intraday ranges.

    Returns list of dicts with: date, open, high, low, close, adjusted_close, volume.
    AdjustedClose == Close (no corporate actions in base scenario).
    """
    rng = np.random.default_rng(seed=42 + seed_offset)
    records = []
    price = initial_price

    # Start from 2024-01-02 (first trading day of 2024)
    from datetime import date, timedelta
    current_date = date(2024, 1, 2)

    for i in range(n_days):
        # Skip weekends
        while current_date.weekday() >= 5:
            current_date += timedelta(days=1)

        # Daily return
        ret = rng.normal(daily_drift, daily_vol)
        close = price * (1 + ret)

        # Intraday range: open near previous close, high/low spread around close
        open_price = price * (1 + rng.normal(0, 0.003))
        intraday_range = abs(close - open_price) + close * rng.uniform(0.005, 0.02)
        high = max(open_price, close) + intraday_range * rng.uniform(0.2, 0.5)
        low = min(open_price, close) - intraday_range * rng.uniform(0.2, 0.5)

        # Ensure OHLC consistency
        high = max(high, open_price, close)
        low = min(low, open_price, close)
        low = max(low, 0.01)  # floor at 1 cent

        volume = int(rng.uniform(500_000, 5_000_000))

        records.append({
            "date": current_date.isoformat(),
            "open": round(float(open_price), 4),
            "high": round(float(high), 4),
            "low": round(float(low), 4),
            "close": round(float(close), 4),
            "adjusted_close": round(float(close), 4),  # No corporate actions
            "volume": volume,
            "dividend_per_share": 0.0,
            "split_coefficient": 1.0,
        })

        price = close
        current_date += timedelta(days=1)

    return records


# ─── Reference Backtest Engine ───────────────────────────────────────────

def python_round_away_from_zero(value: float) -> int:
    """Match C# Math.Round(MidpointRounding.AwayFromZero) for integer rounding."""
    if value >= 0:
        return int(math.floor(value + 0.5))
    else:
        return int(math.ceil(value - 0.5))


def compute_position_size(total_value: float, weight: float,
                          adjusted_close: float) -> int:
    """Compute position size matching C# FixedWeightPositionSizer."""
    if adjusted_close == 0:
        raise ValueError("AdjustedClose is zero")
    desired_value = total_value * weight
    return python_round_away_from_zero(desired_value / adjusted_close)


def run_buy_and_hold_backtest(
    market_data: dict[str, list[dict]],  # ticker -> list of OHLCV records
    weights: dict[str, float],            # ticker -> weight
    initial_cash: float,
    commission_rate: float,
    base_currency: str = "USD",
) -> dict:
    """Run a buy-and-hold backtest matching the C# engine's exact behavior.

    Convention: BuyAndHoldStrategy generates buy signals on day 0.
    Next-bar fill: orders generated on day T fill at day T+1's Open price.
    Commission = fillPrice * quantity * commissionRate.
    FillEventHandler: Buy deducts (price*qty + commission), Sell credits (price*qty - commission).
    Insufficient cash: Buy orders are rejected when cash would go negative.
    """
    # Align dates across all tickers
    all_dates = sorted(set(
        r["date"] for records in market_data.values() for r in records
    ))

    # Build lookup: date -> ticker -> record
    data_by_date: dict[str, dict[str, dict]] = {}
    for ticker, records in market_data.items():
        for r in records:
            data_by_date.setdefault(r["date"], {})[ticker] = r

    # State
    cash = initial_cash
    positions: dict[str, int] = {t: 0 for t in market_data}
    tickers = list(market_data.keys())

    # Pending orders queue (filled on next bar)
    pending_orders: list[dict] = []

    # Outputs
    equity_curve: dict[str, float] = {}
    trades: list[dict] = []
    position_snapshots: dict[str, dict[str, int]] = {}
    cash_snapshots: dict[str, float] = {}

    for day_idx, date_str in enumerate(all_dates):
        day_data = data_by_date.get(date_str, {})

        # Skip if not all tickers have data
        if not all(t in day_data for t in tickers):
            continue

        # --- Process pending orders from previous bar at today's Open ---
        if pending_orders:
            for order in pending_orders:
                ticker = order["ticker"]
                qty = order["quantity"]
                trade_action = order["action"]

                if ticker not in day_data:
                    continue

                # Next-bar fill at Open price
                fill_price = day_data[ticker]["open"]

                # Quantity-limiting for Buy orders (matches zipline's approach):
                # Reduce fill quantity to what cash can afford.
                effective_qty = qty
                if trade_action == "Buy":
                    commission_per_share = fill_price * commission_rate
                    cost_per_share = fill_price + commission_per_share
                    if cost_per_share > 0:
                        max_affordable = int(math.floor(cash / cost_per_share))
                        effective_qty = min(qty, max(max_affordable, 0))
                    if effective_qty <= 0:
                        continue  # Zero-quantity fill: rejected

                commission = fill_price * effective_qty * commission_rate

                # Update cash
                if trade_action == "Buy":
                    cash -= (fill_price * effective_qty + commission)
                else:
                    cash += (fill_price * effective_qty - commission)

                # Update position
                if trade_action == "Buy":
                    positions[ticker] += effective_qty
                else:
                    positions[ticker] -= effective_qty

                trades.append({
                    "date": date_str,
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": effective_qty,
                    "fill_price": fill_price,
                    "commission": commission,
                })

            pending_orders = []

        # --- Generate signals (day 0 only for buy-and-hold) ---
        if day_idx == 0:
            # Day 0: Generate buy signals for all assets (BuyAndHoldStrategy)
            # Compute total value (just cash on day 0, no positions yet)
            total_value = cash

            for ticker in tickers:
                adj_close = day_data[ticker]["adjusted_close"]
                weight = weights[ticker]

                # Compute desired position using AdjustedClose for sizing
                desired = compute_position_size(total_value, weight, adj_close)
                current = positions[ticker]
                order_size = desired - current

                if order_size == 0:
                    continue

                trade_action = "Buy" if order_size > 0 else "Sell"
                qty = abs(order_size)

                # Queue order for next-bar execution
                pending_orders.append({
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": qty,
                })

        # Compute equity: sum(position * adjustedClose) + cash
        equity = cash
        for ticker in tickers:
            adj_close = day_data[ticker]["adjusted_close"]
            equity += positions[ticker] * adj_close

        equity_curve[date_str] = equity
        position_snapshots[date_str] = dict(positions)
        cash_snapshots[date_str] = cash

    # Compute daily returns from equity curve
    equity_values = list(equity_curve.values())
    daily_returns = []
    for i in range(1, len(equity_values)):
        daily_returns.append((equity_values[i] - equity_values[i - 1]) / equity_values[i - 1])

    daily_returns = np.array(daily_returns)

    # Compute tearsheet metrics (same formulas as C# DecimalArrayExtensions)
    metrics = compute_tearsheet_metrics(daily_returns, equity_values, list(equity_curve.keys()))

    return {
        "equity_curve": equity_curve,
        "trades": trades,
        "positions": position_snapshots,
        "cash": cash_snapshots,
        "daily_returns": daily_returns.tolist(),
        "metrics": metrics,
    }


def run_rebalancing_backtest(
    market_data: dict[str, list[dict]],
    weights: dict[str, float],
    initial_cash: float,
    commission_rate: float,
    rebalance_frequency: int,  # Every N days
) -> dict:
    """Run a periodic rebalancing backtest with next-bar Open fills.

    Same as buy-and-hold but re-generates signals every rebalance_frequency days.
    Orders queue on signal day and fill at next bar's Open.
    """
    all_dates = sorted(set(
        r["date"] for records in market_data.values() for r in records
    ))

    data_by_date: dict[str, dict[str, dict]] = {}
    for ticker, records in market_data.items():
        for r in records:
            data_by_date.setdefault(r["date"], {})[ticker] = r

    cash = initial_cash
    positions: dict[str, int] = {t: 0 for t in market_data}
    tickers = list(market_data.keys())

    pending_orders: list[dict] = []
    equity_curve: dict[str, float] = {}
    trades: list[dict] = []
    cash_snapshots: dict[str, float] = {}

    trading_day_count = 0

    for day_idx, date_str in enumerate(all_dates):
        day_data = data_by_date.get(date_str, {})
        if not all(t in day_data for t in tickers):
            continue

        # --- Process pending orders from previous bar at today's Open ---
        if pending_orders:
            for order in pending_orders:
                ticker = order["ticker"]
                qty = order["quantity"]
                trade_action = order["action"]

                if ticker not in day_data:
                    continue

                fill_price = day_data[ticker]["open"]

                # Quantity-limiting for Buy orders
                effective_qty = qty
                if trade_action == "Buy":
                    commission_per_share = fill_price * commission_rate
                    cost_per_share = fill_price + commission_per_share
                    if cost_per_share > 0:
                        max_affordable = int(math.floor(cash / cost_per_share))
                        effective_qty = min(qty, max(max_affordable, 0))
                    if effective_qty <= 0:
                        continue  # Zero-quantity fill: rejected

                commission = fill_price * effective_qty * commission_rate

                if trade_action == "Buy":
                    cash -= (fill_price * effective_qty + commission)
                else:
                    cash += (fill_price * effective_qty - commission)

                if trade_action == "Buy":
                    positions[ticker] += effective_qty
                else:
                    positions[ticker] -= effective_qty

                trades.append({
                    "date": date_str,
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": effective_qty,
                    "fill_price": fill_price,
                    "commission": commission,
                })

            pending_orders = []

        # --- Generate signals on rebalance days ---
        is_rebalance_day = (trading_day_count == 0) or (
            trading_day_count % rebalance_frequency == 0
        )

        if is_rebalance_day:
            # Compute total value: cash + sum(pos * adjClose)
            total_value = cash
            for ticker in tickers:
                adj_close = day_data[ticker]["adjusted_close"]
                total_value += positions[ticker] * adj_close

            for ticker in tickers:
                adj_close = day_data[ticker]["adjusted_close"]
                weight = weights[ticker]

                desired = compute_position_size(total_value, weight, adj_close)
                current = positions[ticker]
                order_size = desired - current

                if order_size == 0:
                    continue

                trade_action = "Buy" if order_size > 0 else "Sell"
                qty = abs(order_size)

                # Queue order for next-bar execution
                pending_orders.append({
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": qty,
                })

        # Compute equity
        equity = cash
        for ticker in tickers:
            adj_close = day_data[ticker]["adjusted_close"]
            equity += positions[ticker] * adj_close

        equity_curve[date_str] = equity
        cash_snapshots[date_str] = cash
        trading_day_count += 1

    equity_values = list(equity_curve.values())
    daily_returns = np.array([
        (equity_values[i] - equity_values[i - 1]) / equity_values[i - 1]
        for i in range(1, len(equity_values))
    ])

    metrics = compute_tearsheet_metrics(daily_returns, equity_values, list(equity_curve.keys()))

    return {
        "equity_curve": equity_curve,
        "trades": trades,
        "cash": cash_snapshots,
        "daily_returns": daily_returns.tolist(),
        "metrics": metrics,
    }


def compute_tearsheet_metrics(daily_returns: np.ndarray,
                              equity_values: list[float],
                              dates: list[str] | None = None) -> dict:
    """Compute the same metrics as C# Backtest.AnalyzePerformanceMetrics()."""
    n = len(daily_returns)
    if n < 2:
        return {}

    rf = 0.0  # Daily risk-free rate = 0

    # Annualized return
    cumulative = np.prod(1 + daily_returns) - 1
    annualized_return = (1 + cumulative) ** (TRADING_DAYS / n) - 1

    # CAGR
    years = n / TRADING_DAYS
    cagr = np.prod(1 + daily_returns) ** (1 / years) - 1

    # Volatility
    daily_vol = np.std(daily_returns, ddof=1)
    annualized_vol = daily_vol * np.sqrt(TRADING_DAYS)

    # Sharpe (annualized)
    daily_sharpe = (np.mean(daily_returns) - rf) / daily_vol
    annualized_sharpe = daily_sharpe * np.sqrt(TRADING_DAYS)

    # Sortino (annualized)
    downside = np.minimum(0, daily_returns - rf)
    dd = np.sqrt(np.sum(downside ** 2) / (n - 1))
    daily_sortino = (np.mean(daily_returns) - rf) / dd if dd > 0 else float("inf")
    annualized_sortino = daily_sortino * np.sqrt(TRADING_DAYS)

    # Max drawdown from equity curve
    equity = np.array(equity_values)
    peak = np.maximum.accumulate(equity)
    drawdowns = (equity - peak) / peak
    max_drawdown = float(np.min(drawdowns))

    # Max drawdown duration — C# uses trading days (equity curve entries, not calendar days)
    max_dd_duration = 0
    start_drawdown_index = 0
    for i, dd_val in enumerate(drawdowns):
        if dd_val >= 0:
            start_drawdown_index = i
        else:
            duration = i - start_drawdown_index
            max_dd_duration = max(max_dd_duration, duration)

    # Calmar
    calmar = cagr / abs(max_drawdown) if max_drawdown != 0 else float("inf")

    # Omega (threshold=0)
    gains = np.sum(np.maximum(daily_returns, 0))
    losses = np.sum(np.maximum(-daily_returns, 0))
    omega = gains / losses if losses > 0 else float("inf")

    # Win rate
    win_rate = float(np.sum(daily_returns > 0) / n)

    # Profit factor
    gross_profit = np.sum(daily_returns[daily_returns > 0])
    gross_loss = abs(np.sum(daily_returns[daily_returns < 0]))
    profit_factor = gross_profit / gross_loss if gross_loss > 0 else float("inf")

    # Recovery factor
    recovery = cumulative / abs(max_drawdown) if max_drawdown != 0 else float("inf")

    # VaR
    sorted_r = np.sort(daily_returns)
    confidence = 0.95
    index = (1 - confidence) * (n - 1)
    lower = int(np.floor(index))
    upper = min(lower + 1, n - 1)
    fraction = index - lower
    hist_var = float(sorted_r[lower] + fraction * (sorted_r[upper] - sorted_r[lower]))

    # Parametric VaR
    z = scipy.stats.norm.ppf(confidence)
    param_var = float(np.mean(daily_returns) - z * daily_vol)

    # CVaR
    tail = daily_returns[daily_returns <= hist_var]
    cvar = float(np.mean(tail)) if len(tail) > 0 else hist_var

    # Skewness (adjusted Fisher-Pearson)
    mean = np.mean(daily_returns)
    std = np.std(daily_returns, ddof=1)
    m3 = np.sum(((daily_returns - mean) / std) ** 3)
    skewness = (n / ((n - 1) * (n - 2))) * m3

    # Kurtosis (sample excess)
    m4 = np.sum(((daily_returns - mean) / std) ** 4)
    kurt_t1 = (n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * m4
    kurt_t2 = 3 * (n - 1) ** 2 / ((n - 2) * (n - 3))
    kurtosis = kurt_t1 - kurt_t2

    return {
        "annualized_return": float(annualized_return),
        "cagr": float(cagr),
        "annualized_volatility": float(annualized_vol),
        "annualized_sharpe_ratio": float(annualized_sharpe),
        "annualized_sortino_ratio": float(annualized_sortino),
        "max_drawdown": float(max_drawdown),
        "max_drawdown_duration": int(max_dd_duration),
        "calmar_ratio": float(calmar),
        "omega_ratio": float(omega),
        "win_rate": float(win_rate),
        "profit_factor": float(profit_factor),
        "recovery_factor": float(recovery),
        "historical_var": hist_var,
        "conditional_var": cvar,
        "skewness": float(skewness),
        "kurtosis": float(kurtosis),
    }


# ─── Vector Generators ──────────────────────────────────────────────────


def gen_single_asset_buy_and_hold():
    """Scenario 1: Single-asset buy-and-hold, no commission."""
    market_data_records = generate_ohlcv("AAPL", 20, 150.0, seed_offset=0)
    market_data = {"AAPL": market_data_records}
    weights = {"AAPL": 1.0}
    initial_cash = 10000.0
    commission_rate = 0.0

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    save("backtest_single_asset", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
            "base_currency": "USD",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Single-asset buy-and-hold. Next-bar Open fill (market order). "
            "No commission. Position = round(cash / price, AwayFromZero). "
            "Equity = position * AdjustedClose + cash. Signals on day 0, fills on day 1."
        ),
    })


def gen_single_asset_with_commission():
    """Scenario 2: Single-asset buy-and-hold with commission."""
    market_data_records = generate_ohlcv("AAPL", 20, 150.0, seed_offset=0)
    market_data = {"AAPL": market_data_records}
    weights = {"AAPL": 1.0}
    initial_cash = 10000.0
    commission_rate = 0.001  # 0.1%

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    save("backtest_single_asset_commission", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
            "base_currency": "USD",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Single-asset buy-and-hold with 0.1% commission. "
            "Next-bar Open fill. Commission = fillPrice * quantity * 0.001. "
            "Buy deducts: -(price*qty + commission). Cash remainder is lower."
        ),
    })


def gen_multi_asset_fixed_weights():
    """Scenario 3: Multi-asset fixed weight buy-and-hold."""
    aapl_data = generate_ohlcv("AAPL", 60, 150.0, seed_offset=0)
    msft_data = generate_ohlcv("MSFT", 60, 380.0, seed_offset=100)

    market_data = {"AAPL": aapl_data, "MSFT": msft_data}
    weights = {"AAPL": 0.6, "MSFT": 0.4}
    initial_cash = 100000.0
    commission_rate = 0.001

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    save("backtest_multi_asset", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
            "base_currency": "USD",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Multi-asset buy-and-hold: 60% AAPL, 40% MSFT. "
            "Position sizing uses total portfolio value * weight / adjClose. "
            "Rounding: AwayFromZero. Cash remainder varies by rounding."
        ),
    })


def gen_commission_impact():
    """Scenario 4: Same strategy with vs without commission — demonstrates impact."""
    market_data_records = generate_ohlcv("AAPL", 60, 150.0, seed_offset=0)
    market_data = {"AAPL": market_data_records}
    weights = {"AAPL": 1.0}
    initial_cash = 10000.0

    result_no_comm = run_buy_and_hold_backtest(market_data, weights, initial_cash, 0.0)
    result_with_comm = run_buy_and_hold_backtest(market_data, weights, initial_cash, 0.001)

    save("backtest_commission_impact", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rates": [0.0, 0.001],
            "strategy": "BuyAndHold",
        },
        "expected": {
            "no_commission": result_no_comm,
            "with_commission": result_with_comm,
        },
        "library": f"numpy {np.__version__}",
        "notes": (
            "Commission impact comparison. Same market data, different commission rates. "
            "Commission reduces initial position and/or leaves more cash remainder. "
            "Equity curves diverge from day 0."
        ),
    })


def gen_periodic_rebalancing():
    """Scenario 5: Multi-asset with periodic rebalancing (monthly = 21 days)."""
    aapl_data = generate_ohlcv("AAPL", 126, 150.0, seed_offset=0)  # ~6 months
    msft_data = generate_ohlcv("MSFT", 126, 380.0, seed_offset=100)

    market_data = {"AAPL": aapl_data, "MSFT": msft_data}
    weights = {"AAPL": 0.6, "MSFT": 0.4}
    initial_cash = 100000.0
    commission_rate = 0.001
    rebalance_every = 21  # Monthly

    result = run_rebalancing_backtest(
        market_data, weights, initial_cash, commission_rate, rebalance_every
    )

    save("backtest_rebalancing", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "rebalance_frequency_days": rebalance_every,
            "strategy": "RebalancingBuyAndHold",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Monthly rebalancing (every 21 trading days). "
            "Sells overweight, buys underweight on rebalance days. "
            "Commission charged on each rebalance trade."
        ),
    })


def gen_drawdown_recovery():
    """Scenario 6: Bear market data to verify drawdown metrics."""
    # Generate a bear market: initial rally, then crash, partial recovery
    n_days = 126
    rng = np.random.default_rng(seed=99)
    from datetime import date, timedelta

    records = []
    price = 100.0
    current_date = date(2024, 1, 2)

    for i in range(n_days):
        while current_date.weekday() >= 5:
            current_date += timedelta(days=1)

        # Phase 1 (0-30): mild rally
        if i < 30:
            ret = rng.normal(0.002, 0.01)
        # Phase 2 (30-70): crash
        elif i < 70:
            ret = rng.normal(-0.008, 0.02)
        # Phase 3 (70-100): partial recovery
        elif i < 100:
            ret = rng.normal(0.005, 0.015)
        # Phase 4 (100-126): flat
        else:
            ret = rng.normal(0.0001, 0.008)

        close = price * (1 + ret)
        open_price = price * (1 + rng.normal(0, 0.003))
        spread = abs(close - open_price) + close * rng.uniform(0.005, 0.02)
        high = max(open_price, close) + spread * rng.uniform(0.2, 0.5)
        low = min(open_price, close) - spread * rng.uniform(0.2, 0.5)
        high = max(high, open_price, close)
        low = max(min(low, open_price, close), 0.01)

        records.append({
            "date": current_date.isoformat(),
            "open": round(float(open_price), 4),
            "high": round(float(high), 4),
            "low": round(float(low), 4),
            "close": round(float(close), 4),
            "adjusted_close": round(float(close), 4),
            "volume": int(rng.uniform(500_000, 5_000_000)),
            "dividend_per_share": 0.0,
            "split_coefficient": 1.0,
        })

        price = close
        current_date += timedelta(days=1)

    market_data = {"BEAR": records}
    weights = {"BEAR": 1.0}
    initial_cash = 10000.0
    commission_rate = 0.0

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    save("backtest_drawdown", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Bear market scenario: rally → crash → partial recovery → flat. "
            "Verifies MaxDrawdown depth and duration. "
            "No commission to isolate drawdown calculation from cost effects."
        ),
    })


def gen_position_sizing_rounding():
    """Scenario 7: Verify position sizing rounding (AwayFromZero)."""
    # Use prices that produce fractional positions: $33.33 → 10000/33.33 = 300.03
    # Open == AdjustedClose so quantity-limiting doesn't interfere with the rounding test.
    from datetime import date, timedelta
    records = []
    current_date = date(2024, 1, 2)

    # 5 days with prices designed to test rounding; open == close == adjusted_close
    prices = [33.33, 33.33, 33.25, 33.75, 33.10]
    for i, px in enumerate(prices):
        while current_date.weekday() >= 5:
            current_date += timedelta(days=1)

        records.append({
            "date": current_date.isoformat(),
            "open": px,
            "high": px + 0.50,
            "low": px - 0.50,
            "close": px,
            "adjusted_close": px,
            "volume": 1000000,
            "dividend_per_share": 0.0,
            "split_coefficient": 1.0,
        })
        current_date += timedelta(days=1)

    market_data = {"ODD": records}
    weights = {"ODD": 1.0}
    initial_cash = 10000.0
    commission_rate = 0.0

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    # Manually verify: 10000 / 33.33 = 300.03 → rounds to 300 (AwayFromZero)
    # Day 1 Open == $33.33, so 300 * 33.33 = 9999 <= 10000 → no quantity-limiting.
    expected_position = python_round_away_from_zero(10000.0 / 33.33)

    save("backtest_rounding", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
        },
        "expected": {
            **result,
            "expected_initial_position": expected_position,
            "rounding_detail": {
                "raw_value": 10000.0 / 33.33,
                "rounded": expected_position,
                "method": "MidpointRounding.AwayFromZero",
            },
        },
        "library": f"numpy {np.__version__}",
        "notes": (
            "Position sizing rounding verification. "
            "10000 / 33.33 = 300.03 → rounds to 300 (AwayFromZero). "
            "Cash remainder = 10000 - 300 * 33.33 = 10000 - 9999 = 1.0."
        ),
    })


def gen_benchmark_relative_metrics():
    """Scenario 8: Portfolio vs benchmark for Alpha/Beta/IR verification."""
    # Portfolio: aggressive asset
    portfolio_data = generate_ohlcv("AGG", 252, 100.0, daily_drift=0.0005,
                                     daily_vol=0.02, seed_offset=200)
    # Benchmark: market proxy
    benchmark_data = generate_ohlcv("MKT", 252, 100.0, daily_drift=0.0003,
                                     daily_vol=0.012, seed_offset=300)

    portfolio_md = {"AGG": portfolio_data}
    benchmark_md = {"MKT": benchmark_data}

    port_result = run_buy_and_hold_backtest(
        portfolio_md, {"AGG": 1.0}, 10000.0, 0.0
    )
    bench_result = run_buy_and_hold_backtest(
        benchmark_md, {"MKT": 1.0}, 10000.0, 0.0
    )

    # Compute relative metrics from daily returns
    port_dr = np.array(port_result["daily_returns"])
    bench_dr = np.array(bench_result["daily_returns"])

    # Align lengths (should be same but defensive)
    min_len = min(len(port_dr), len(bench_dr))
    port_dr = port_dr[:min_len]
    bench_dr = bench_dr[:min_len]

    # Beta
    cov_pb = np.sum((port_dr - np.mean(port_dr)) * (bench_dr - np.mean(bench_dr))) / (min_len - 1)
    var_b = np.sum((bench_dr - np.mean(bench_dr)) ** 2) / (min_len - 1)
    beta = cov_pb / var_b

    # Alpha
    rf = 0.0
    alpha = np.mean(port_dr) - rf - beta * (np.mean(bench_dr) - rf)

    # Information Ratio
    active = port_dr - bench_dr
    ir = np.mean(active) / np.std(active, ddof=1)

    save("backtest_benchmark", {
        "inputs": {
            "portfolio_market_data": portfolio_md,
            "benchmark_market_data": benchmark_md,
            "portfolio_weights": {"AGG": 1.0},
            "benchmark_weights": {"MKT": 1.0},
            "initial_cash": 10000.0,
            "commission_rate": 0.0,
        },
        "expected": {
            "portfolio": port_result,
            "benchmark": bench_result,
            "relative_metrics": {
                "beta": float(beta),
                "alpha": float(alpha),
                "information_ratio": float(ir),
            },
        },
        "library": f"numpy {np.__version__}",
        "notes": (
            "Portfolio vs benchmark for relative metrics. "
            "Alpha = mean(port) - rf - beta * (mean(bench) - rf). "
            "Beta = cov(port,bench) / var(bench), ddof=1. "
            "IR = mean(active) / std(active, ddof=1). All daily, not annualized."
        ),
    })


def gen_cash_remainder_precision():
    """Scenario 9: Verify cash remainder after position sizing is exact."""
    # Use enough days with realistic volatility to avoid degenerate metrics
    # (e.g., Sortino undefined when all returns are positive)
    market_data_records = generate_ohlcv("EXACT", 60, 100.0, daily_drift=0.0002,
                                          daily_vol=0.012, seed_offset=500)

    market_data = {"EXACT": market_data_records}
    weights = {"EXACT": 1.0}
    initial_cash = 10000.0
    commission_rate = 0.001

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    # The first price is ~$100 so:
    # Position = round(10000 / ~100) = ~100 shares
    # Commission = ~100 * ~100 * 0.001 = ~10
    # Cash = 10000 - (~10000 + ~10) ≈ -10 (negative is valid)

    save("backtest_cash_precision", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Cash remainder precision test. Price=$100, 100 shares. "
            "Commission = $10. Cash = 10000 - 10010 = -10. "
            "Negative cash is valid (commission exceeds remainder). "
            "Equity = 100*price + (-10)."
        ),
    })


def gen_multi_asset_three_way():
    """Scenario 10: Three-asset portfolio with unequal weights (252 days)."""
    aapl = generate_ohlcv("AAPL", TRADING_DAYS, 150.0, seed_offset=0)
    msft = generate_ohlcv("MSFT", TRADING_DAYS, 380.0, seed_offset=100)
    goog = generate_ohlcv("GOOG", TRADING_DAYS, 140.0, daily_drift=0.0004,
                           daily_vol=0.018, seed_offset=400)

    market_data = {"AAPL": aapl, "MSFT": msft, "GOOG": goog}
    weights = {"AAPL": 0.5, "MSFT": 0.3, "GOOG": 0.2}
    initial_cash = 500000.0
    commission_rate = 0.001

    result = run_buy_and_hold_backtest(market_data, weights, initial_cash, commission_rate)

    save("backtest_three_asset", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "strategy": "BuyAndHold",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Three-asset buy-and-hold: 50% AAPL, 30% MSFT, 20% GOOG. "
            "252 trading days — full year for annualized metric accuracy. "
            "Commission 0.1%. Verifies complete tearsheet metrics."
        ),
    })


def run_calendar_rebalancing_backtest(
    market_data: dict[str, list[dict]],
    weights: dict[str, float],
    initial_cash: float,
    commission_rate: float,
    rebalance_months: int,  # 3 for quarterly, 1 for monthly, 12 for annually
) -> dict:
    """Run a calendar-based rebalancing backtest matching C# ConstructionModelStrategy.

    Rebalances on day 0, then every N calendar months from the first rebalance date
    (using timestamp >= lastRebalanceDate + N months). Matches C# IsRebalancingDate.
    """
    from datetime import date

    all_dates = sorted(set(
        r["date"] for records in market_data.values() for r in records
    ))

    data_by_date: dict[str, dict[str, dict]] = {}
    for ticker, records in market_data.items():
        for r in records:
            data_by_date.setdefault(r["date"], {})[ticker] = r

    cash = initial_cash
    positions: dict[str, int] = {t: 0 for t in market_data}
    tickers = list(market_data.keys())

    pending_orders: list[dict] = []
    equity_curve: dict[str, float] = {}
    trades: list[dict] = []
    cash_snapshots: dict[str, float] = {}

    last_rebalance_date = None

    def add_months(d: date, months: int) -> date:
        """Add months to a date, clamping to end of month."""
        month = d.month - 1 + months
        year = d.year + month // 12
        month = month % 12 + 1
        import calendar
        day = min(d.day, calendar.monthrange(year, month)[1])
        return date(year, month, day)

    for day_idx, date_str in enumerate(all_dates):
        day_data = data_by_date.get(date_str, {})
        if not all(t in day_data for t in tickers):
            continue

        current_date = date.fromisoformat(date_str)

        # --- Process pending orders from previous bar at today's Open ---
        if pending_orders:
            for order in pending_orders:
                ticker = order["ticker"]
                qty = order["quantity"]
                trade_action = order["action"]

                if ticker not in day_data:
                    continue

                fill_price = day_data[ticker]["open"]

                effective_qty = qty
                if trade_action == "Buy":
                    commission_per_share = fill_price * commission_rate
                    cost_per_share = fill_price + commission_per_share
                    if cost_per_share > 0:
                        max_affordable = int(math.floor(cash / cost_per_share))
                        effective_qty = min(qty, max(max_affordable, 0))
                    if effective_qty <= 0:
                        continue

                commission = fill_price * effective_qty * commission_rate

                if trade_action == "Buy":
                    cash -= (fill_price * effective_qty + commission)
                else:
                    cash += (fill_price * effective_qty - commission)

                if trade_action == "Buy":
                    positions[ticker] += effective_qty
                else:
                    positions[ticker] -= effective_qty

                trades.append({
                    "date": date_str,
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": effective_qty,
                    "fill_price": fill_price,
                    "commission": commission,
                })

            pending_orders = []

        # --- Calendar-based rebalancing (matches C# IsRebalancingDate) ---
        is_rebalance_day = False
        if last_rebalance_date is None:
            is_rebalance_day = True  # First day
        else:
            next_rebalance = add_months(last_rebalance_date, rebalance_months)
            if current_date >= next_rebalance:
                is_rebalance_day = True

        if is_rebalance_day:
            total_value = cash
            for ticker in tickers:
                adj_close = day_data[ticker]["adjusted_close"]
                total_value += positions[ticker] * adj_close

            for ticker in tickers:
                adj_close = day_data[ticker]["adjusted_close"]
                weight = weights[ticker]

                desired = compute_position_size(total_value, weight, adj_close)
                current = positions[ticker]
                order_size = desired - current

                if order_size == 0:
                    continue

                trade_action = "Buy" if order_size > 0 else "Sell"
                qty = abs(order_size)

                pending_orders.append({
                    "ticker": ticker,
                    "action": trade_action,
                    "quantity": qty,
                })

            last_rebalance_date = current_date

        # Compute equity
        equity = cash
        for ticker in tickers:
            adj_close = day_data[ticker]["adjusted_close"]
            equity += positions[ticker] * adj_close

        equity_curve[date_str] = equity
        cash_snapshots[date_str] = cash

    equity_values = list(equity_curve.values())
    daily_returns = np.array([
        (equity_values[i] - equity_values[i - 1]) / equity_values[i - 1]
        for i in range(1, len(equity_values))
    ])

    metrics = compute_tearsheet_metrics(daily_returns, equity_values, list(equity_curve.keys()))

    return {
        "equity_curve": equity_curve,
        "trades": trades,
        "cash": cash_snapshots,
        "daily_returns": daily_returns.tolist(),
        "metrics": metrics,
    }


def gen_equal_weight_construction():
    """Scenario 11: Equal weight construction model with quarterly (calendar) rebalancing.

    Uses calendar-based rebalancing to match C# ConstructionModelStrategy.IsRebalancingDate:
    first trade on day 0, then every 3 calendar months (AddMonths(3)).

    The C# test wires ConstructionModelStrategy + DynamicWeightPositionSizer +
    EqualWeightConstruction. Since EqualWeight always returns 1/N, the Python
    calendar-rebalancing engine with equal weights produces identical results.
    """
    aapl_data = generate_ohlcv("AAPL", 252, 150.0, seed_offset=0)
    msft_data = generate_ohlcv("MSFT", 252, 380.0, seed_offset=100)
    googl_data = generate_ohlcv("GOOGL", 252, 140.0, seed_offset=200)

    market_data = {"AAPL": aapl_data, "MSFT": msft_data, "GOOGL": googl_data}
    n_assets = len(market_data)
    equal_weight = 1.0 / n_assets
    weights = {t: equal_weight for t in market_data}
    initial_cash = 100000.0
    commission_rate = 0.001

    result = run_calendar_rebalancing_backtest(
        market_data, weights, initial_cash, commission_rate,
        rebalance_months=3  # Quarterly
    )

    save("backtest_equal_weight_construction", {
        "inputs": {
            "market_data": market_data,
            "weights": weights,
            "initial_cash": initial_cash,
            "commission_rate": commission_rate,
            "rebalance_months": 3,
            "construction_model": "EqualWeight",
            "strategy": "ConstructionModelStrategy",
        },
        "expected": result,
        "library": f"numpy {np.__version__}",
        "notes": (
            "Equal weight construction model with calendar-based quarterly rebalancing. "
            "3 assets (AAPL, MSFT, GOOGL), each weighted 1/3. "
            "Rebalances on day 0, then every 3 calendar months (matching C# AddMonths(3)). "
            "C# test uses ConstructionModelStrategy + DynamicWeightPositionSizer pipeline."
        ),
    })


# ═══════════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════════

def main():
    print("Generating backtest verification vectors...")
    print()

    print("Scenario 1: Single-asset buy-and-hold (no commission)")
    gen_single_asset_buy_and_hold()

    print("Scenario 2: Single-asset buy-and-hold (with commission)")
    gen_single_asset_with_commission()

    print("Scenario 3: Multi-asset fixed weights")
    gen_multi_asset_fixed_weights()

    print("Scenario 4: Commission impact comparison")
    gen_commission_impact()

    print("Scenario 5: Periodic rebalancing")
    gen_periodic_rebalancing()

    print("Scenario 6: Drawdown & recovery")
    gen_drawdown_recovery()

    print("Scenario 7: Position sizing rounding")
    gen_position_sizing_rounding()

    print("Scenario 8: Benchmark relative metrics")
    gen_benchmark_relative_metrics()

    print("Scenario 9: Cash remainder precision")
    gen_cash_remainder_precision()

    print("Scenario 10: Three-asset full-year portfolio")
    gen_multi_asset_three_way()

    print("Scenario 11: Equal weight construction model with quarterly rebalancing")
    gen_equal_weight_construction()

    bt_vectors = [f for f in VECTORS_DIR.glob("backtest_*.json")]
    print()
    print(f"Done. {len(bt_vectors)} backtest vector files in {VECTORS_DIR}")


if __name__ == "__main__":
    main()
