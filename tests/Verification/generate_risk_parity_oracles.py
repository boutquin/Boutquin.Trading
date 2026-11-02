#!/usr/bin/env python3
"""Independent 80-digit simultaneous Newton / active-set ERC controls.

No coordinate descent and no Trading optimizer replica. Frozen return panels in
construction_risk_parity.json are retained byte-for-byte as values. Recompute only
the previously defective weights and derived contributions. Requires mpmath.
"""
import itertools
import json
import random
from pathlib import Path
import mpmath as mp

mp.mp.dps = 80
VECTORS = Path(__file__).parent / 'vectors'


def erc(cov):
    n = len(cov)
    a = mp.matrix(cov)
    x = mp.matrix([1 / mp.sqrt(a[i, i]) for i in range(n)])
    def objective(y):
        return (y.T*a*y)[0]/2-sum(mp.log(value) for value in y)
    for _ in range(200):
        ax = a*x
        gradient = mp.matrix([ax[i]-1/x[i] for i in range(n)])
        if max(abs(x[i]*ax[i]-1) for i in range(n)) < mp.mpf('1e-65'):
            break
        hessian = a + mp.diag([1/(value*value) for value in x])
        step = mp.lu_solve(hessian, -gradient)
        rate = mp.mpf(1)
        old = objective(x)
        slope = (gradient.T*step)[0]
        for _ in range(200):
            candidate = x+rate*step
            if min(candidate) > 0 and objective(candidate) <= old+mp.mpf('0.0001')*rate*slope:
                x = candidate
                break
            rate /= 2
        else:
            raise ArithmeticError('Independent Newton line search exhausted')
    else:
        raise ArithmeticError('Independent Newton iterations exhausted')
    assert min(x) > 0
    w = x / sum(x)
    rc = [w[i] * (a * w)[i] for i in range(n)]
    assert max(abs(r / sum(rc) - mp.mpf(1) / n) for r in rc) < mp.mpf('1e-60')
    return list(w)


def bounded(cov, lower, upper):
    n = len(cov)
    a = mp.matrix(cov)
    lo, hi = mp.mpf(str(lower)), mp.mpf(str(upper))
    for state in itertools.product((-1, 0, 1), repeat=n):
        free = [i for i in range(n) if state[i] == 0]
        if not free:
            continue
        fixed = {i: lo if state[i] == -1 else hi for i in range(n) if state[i] != 0}
        remainder = 1 - sum(fixed.values())
        if not len(free)*lo < remainder < len(free)*hi:
            continue
        def unpack(x):
            w = mp.matrix([fixed.get(i, 0) for i in range(n)])
            for k, i in enumerate(free):
                w[i] = x[k]
            return w
        def equations(*x):
            w = unpack(x)
            marginal = a*w
            return [w[i]*marginal[i]-x[-1] for i in free] + [sum(w)-1]
        start = [remainder/len(free)]*len(free) + [mp.mpf('0.1')]
        try:
            x = mp.findroot(equations, start, tol=mp.mpf('1e-65'), maxsteps=200)
        except (ValueError, ZeroDivisionError):
            continue
        w = unpack(x)
        c = x[-1]
        if c <= 0 or min(w) < lo or max(w) > hi:
            continue
        residuals = [w[i]*(a*w)[i]-c for i in range(n)]
        if any(state[i] == -1 and residuals[i] < -mp.mpf('1e-60') or
               state[i] == 1 and residuals[i] > mp.mpf('1e-60') for i in fixed):
            continue
        assert max(abs(r) for i, r in enumerate(residuals) if i in free) < mp.mpf('1e-60')
        return list(w)
    raise ArithmeticError('No positive-multiplier box risk-budget solution found')


def main():
    original_path = VECTORS / 'construction_risk_parity.json'
    original = json.loads(original_path.read_text())
    for case in original['cases'].values():
        series = [[mp.mpf(str(x)) for x in row] for row in case['returns']]
        n, t = len(series), len(series[0])
        means = [sum(row)/t for row in series]
        cov = [[sum((series[i][k]-means[i])*(series[j][k]-means[j]) for k in range(t))/(t-1)
                for j in range(n)] for i in range(n)]
        w = erc(cov)
        rc = [w[i]*sum(cov[i][j]*w[j] for j in range(n)) for i in range(n)]
        case['weights'] = [float(x) for x in w]
        case['risk_contributions'] = [float(x) for x in rc]
        case['risk_contrib_max_deviation'] = float(max(abs(x-sum(rc)/n) for x in rc))
    original['notes'] = 'Independent 80-digit simultaneous Newton ERC oracle; frozen returns unchanged. No inverse-MRC replica.'
    original_path.write_text(json.dumps(original, indent=2)+'\n')

    rng = random.Random(20261028)
    cases = []
    for n in (3, 4, 6, 8):
        b = [[rng.randrange(-8, 9) for _ in range(n)] for _ in range(n)]
        cov = [[sum(b[k][i]*b[k][j] for k in range(n)) + (30 if i == j else 0)
                for j in range(n)] for i in range(n)]
        w = erc(cov)
        cases.append({'name':f'held_out_spd_{n}', 'covariance':cov,
                      'min':0, 'max':1, 'weights':[mp.nstr(x, 70) for x in w]})
    for name, cov, lo, hi in [
        ('binding_cap', [[1,0,0],[0,4,0],[0,0,9]],0,0.4),
        ('binding_floor', [[1,0,0],[0,4,0],[0,0,100]],0.2,1),
        ('correlated_bounds', [[4,-1,1],[-1,9,2],[1,2,25]],0.2,0.45),
        ('four_asset_bounds', [[1,0,0,0],[0,4,0,0],[0,0,16,0],[0,0,0,64]],0.15,0.4),
    ]:
        w=bounded(cov,lo,hi)
        cases.append({'name':name, 'covariance':cov,'min':lo,'max':hi,
                      'weights':[mp.nstr(x,70) for x in w]})
    (VECTORS/'risk_parity_independent.json').write_text(json.dumps({
        'oracle':'mpmath 80-digit simultaneous Newton; bounds via active-set enumeration and KKT signs',
        'seed':20261028,'cases':cases},indent=2)+'\n')
    print(f'Validated and wrote {len(cases)} independent cases plus 3 frozen-return panels.')

if __name__ == '__main__':
    main()
