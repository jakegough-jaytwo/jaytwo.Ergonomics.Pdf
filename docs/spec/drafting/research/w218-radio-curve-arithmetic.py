"""Exact rational candidate-dot proof; no PDF reader or target-library execution."""
import hashlib
import json
import platform
import sys
from fractions import Fraction as F
from pathlib import Path


def cross(a, b):
    return a[0] * b[1] - a[1] * b[0]


def power(points):
    p, q, r, s = points
    return [p, tuple(3 * (q[i] - p[i]) for i in range(2)),
            tuple(3 * (p[i] - 2 * q[i] + r[i]) for i in range(2)),
            tuple(-p[i] + 3 * q[i] - 3 * r[i] + s[i] for i in range(2))]


def twice_area(points):
    coefficients = power(points)
    return sum((j * cross(a, b) / F(i + j)
                for i, a in enumerate(coefficients)
                for j, b in enumerate(coefficients) if j), F(0))


def main():
    k = F('0.5522847498')
    quarter = [(F(1), F(0)), (F(1), k), (k, F(1)), (F(0), F(1))]
    curves = [quarter]
    for _ in range(3):
        curves.append([(-y, x) for x, y in curves[-1]])
    # Each derivative is a quadratic Bernstein sum of nonnegative (or
    # nonpositive) differences. Thus no interior extremum exceeds endpoints.
    monotone = all(all(d >= 0 for d in differences) or all(d <= 0 for d in differences)
                   for curve in curves for axis in range(2)
                   for differences in [[curve[i + 1][axis] - curve[i][axis] for i in range(3)]])
    closed = all(curves[i][-1] == curves[(i + 1) % 4][0] for i in range(4))
    area = sum(map(twice_area, curves), F(0)) / 2
    rows = []
    for x, y, w, h in [(0, 0, 20, 20), (3, -7, 40, 10), (-5, 9, 10, 40), (0, 0, 1, 1)]:
        cx, cy, radius = F(x) + F(w, 2), F(y) + F(h, 2), F(min(w, h), 5)
        bounds = [cx - radius, cy - radius, cx + radius, cy + radius]
        rows.append({'content_rect': [x, y, w, h], 'exact_bounds': list(map(str, bounds)),
                     'exact_area': str(area * radius * radius),
                     'contained': bounds[0] >= x and bounds[1] >= y and bounds[2] <= x + w and bounds[3] <= y + h})
    # A changed control factor can overshoot even with unchanged endpoints.
    bad = [(F(1), F(0)), (F(1), F(2)), (F(2), F(1)), (F(0), F(1))]
    t = F(1, 2)
    bad_midpoint = [sum(power(bad)[i][axis] * t ** i for i in range(4)) for axis in range(2)]
    overshoots = max(bad_midpoint) > 1
    ok = monotone and closed and area > 0 and all(r['contained'] for r in rows) and overshoots
    result = {'classification': 'Independent exact arithmetic of a proposed fixed cubic template, not rendering or representative acceptance',
              'python': sys.version, 'platform': platform.platform(),
              'script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'control_factor': str(k), 'quarter_controls': [[str(x), str(y)] for x, y in quarter],
              'coordinate_monotonicity': monotone, 'closed': closed, 'unit_radius_exact_area': str(area),
              'cases': rows, 'altered_control_midpoint': list(map(str, bad_midpoint)),
              'altered_control_overshoots': overshoots, 'all_arithmetic_oracles_match': ok}
    Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'cases': len(rows), 'monotone': monotone, 'positive_area': area > 0, 'altered_control_overshoots': overshoots, 'ok': ok}))
    return 0 if ok else 1


if __name__ == '__main__':
    raise SystemExit(main())
