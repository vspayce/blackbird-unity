# Enlarging the Hopkins house without stretching what's in it.
#
# The prototype's house (build_hopkins.py, hopkins_house.py) is laid out in hundreds of literal coordinates. Rather than
# rewrite it, everything is built as before and then carried through a warp: a piecewise-linear map, separate in x, y
# and z, that stretches the middles of rooms and slides the walls apart. Bands about each wall line only translate, so
# a wall keeps its thickness and whatever hangs on it stays on it.
#
#   across (x): the hall's half-width 7 -> 11, the side rooms 8 -> 10 wide, the house 30 -> 44 m
#   deep (z):   the hall 16 -> 28 m, the vestibule 4 -> 6, the rooms behind 8 -> 10, the house 28 -> 44 m
#   up (y):     everything above the terrace 1.2x (rooms 5.5 -> 6.6 m, the gallery 6 -> 7.2, the tower 22 -> 26.4)
#
# Furniture, paintings, lamps and the like are built inside a "rigid" context: they move with the warp (taken at their
# foot, or at the top for things that hang) but keep their size. Cylinders stay round (only their axis is warped) and
# spheres just move. The lists that go out to the game (lamps, colliders, rooms, places to use) are warped to match.
import math
from mathutils import Vector
import setkit

X_BREAKS = [(0.0, 0.0), (5.8, 9.8), (8.2, 12.2), (13.8, 19.8), (16.2, 22.2)]           # |x|: orig -> new
Z_BREAKS = [(-15.2, -17.2), (-12.8, -14.8), (-7.2, -7.2), (-4.8, -4.8), (0.8, 6.8), (3.2, 9.2),
            (8.8, 20.8), (11.2, 23.2), (12.8, 26.8), (15.2, 29.2)]
Y_SCALE = 1.2


def _pw(breaks):
    xs = [b[0] for b in breaks]; ys = [b[1] for b in breaks]
    def f(v):
        if v <= xs[0]: return ys[0] + (v - xs[0])
        if v >= xs[-1]: return ys[-1] + (v - xs[-1])
        for i in range(len(xs) - 1):
            if v <= xs[i + 1]:
                t = (v - xs[i]) / (xs[i + 1] - xs[i])
                return ys[i] + t * (ys[i + 1] - ys[i])
    return f


_fx = _pw(X_BREAKS)
fz = _pw(Z_BREAKS)
def fx(v): return math.copysign(_fx(abs(v)), v)
def fy(v): return v * Y_SCALE if v > 0 else v
def W(p): return Vector((fx(p[0]), fy(p[1]), fz(p[2])))


STATE = dict(ctx=None, done=False, recs=[])
DONE = set()           # (id(list), index) of list entries already moved with a rigid piece


class Tracked(list):
    """A list (lamps, colliders) that notes which entries were added while a rigid piece was being built."""
    def append(self, item):
        super().append(item)
        if STATE['ctx'] is not None:
            STATE['ctx']['items'].append((self, len(self) - 1))


def _rigid(fn, hang=False):
    def w(*a, **kw):
        if STATE['ctx'] is not None or STATE['done']:
            return fn(*a, **kw)
        ctx = dict(start={m: len(bm.verts) for m, bm in setkit.BM.items()}, items=[])
        STATE['ctx'] = ctx
        try:
            r = fn(*a, **kw)
        finally:
            STATE['ctx'] = None
        lo = [math.inf] * 3; hi = [-math.inf] * 3; ranges = []
        for m, bm in setkit.BM.items():
            n0, n1 = ctx['start'].get(m, 0), len(bm.verts)
            if n1 <= n0: continue
            ranges.append((m, n0, n1))
            bm.verts.ensure_lookup_table()
            for i in range(n0, n1):
                co = bm.verts[i].co
                for k in range(3):
                    if co[k] < lo[k]: lo[k] = co[k]
                    if co[k] > hi[k]: hi[k] = co[k]
        if ranges:
            anchor = Vector(((lo[0] + hi[0]) / 2, hi[1] if hang else lo[1], (lo[2] + hi[2]) / 2))
        else:
            anchor = Vector((0, 0, 0))
        d = W(anchor) - anchor
        STATE['recs'].append(('rigid', ranges, d))
        for lst, i in ctx['items']:
            lst[i] = _shift(lst[i], d)
            DONE.add((id(lst), i))
        return r
    w.__wrapped__ = fn
    return w


def _shift(item, d):
    if len(item) == 3: return [item[0] + d.x, item[1] + d.y, item[2] + d.z]
    if len(item) == 6: return [item[0] + d.x, item[1] + d.y, item[2] + d.z, item[3] + d.x, item[4] + d.y, item[5] + d.z]
    return item


_orig = {}


def _cyl(mat, base, height, r0, r1=None, seg=12, axis=setkit.UP, cap=True):
    if STATE['ctx'] is not None or STATE['done']:
        return _orig['cyl'](mat, base, height, r0, r1, seg, axis, cap)
    bm = setkit.bm_for(mat); n0 = len(bm.verts)
    _orig['cyl'](mat, base, height, r0, r1, seg, axis, cap)
    STATE['recs'].append(('axis', [(mat, n0, len(bm.verts))], (Vector(base), Vector(axis).normalized(), height)))


def _sphere(mat, c, r, seg=10):
    if STATE['ctx'] is not None or STATE['done']:
        return _orig['sphere'](mat, c, r, seg)
    bm = setkit.bm_for(mat); n0 = len(bm.verts)
    _orig['sphere'](mat, c, r, seg)
    STATE['recs'].append(('point', [(mat, n0, len(bm.verts))], Vector(c)))


RIGID = ['chair', 'table', 'bust', 'sofa', 'wingback', 'pedestal_table', 'longcase_clock', 'amphora', 'plinth',
         'umbrella_stand', 'globe', 'library_ladder', 'card_catalogue', 'grand_piano', 'easel', 'frame_painting', 'statue',
         'donkey', 'four_poster', 'cabinet', 'modelling_stand', 'brougham', 'fountain', 'urn', 'fireplace', 'sconce',
         'cable_car', 'iron_gate_leaf', 'palm', 'fern', 'grand_palm', 'grand_fern', 'eastlake_bench', 'torchere_figure',
         'marble_fountain', 'crate', 'ash_can']     # (not 'barrel': in the palm house that's a glass roof)
HANG = ['gasolier', 'room_gasolier', 'hanging_lantern']


def install(modules, K, G=None):
    """Wrap the primitives and the furniture builders in every module that holds them, and track the lists."""
    _orig['cyl'], _orig['sphere'] = setkit.cyl, setkit.sphere
    for mod in modules:
        for name, new in (('cyl', _cyl), ('sphere', _sphere)):
            if getattr(mod, name, None) is _orig[name]: setattr(mod, name, new)
        for name in RIGID + HANG:
            f = getattr(mod, name, None)
            if callable(f) and not hasattr(f, '__wrapped__'):
                setattr(mod, name, _rigid(f, hang=name in HANG))
    K.LAMPS = Tracked(K.LAMPS); K.COLLIDERS = Tracked(K.COLLIDERS)
    if G is not None: G.BIG_LAMPS = Tracked(G.BIG_LAMPS)


def apply(K, G=None):
    """Carry everything built so far through the warp. Anything built afterwards is in the new coordinates."""
    covered = {m: bytearray(len(bm.verts)) for m, bm in setkit.BM.items()}
    for kind, ranges, data in STATE['recs']:
        for m, n0, n1 in ranges:
            bm = setkit.BM[m]; bm.verts.ensure_lookup_table()
            cov = covered[m]
            if kind == 'rigid':
                for i in range(n0, n1):
                    bm.verts[i].co += data; cov[i] = 1
            elif kind == 'point':
                d = W(data) - data
                for i in range(n0, n1):
                    bm.verts[i].co += d; cov[i] = 1
            else:
                a, ax, h = data
                Wa = W(a)
                if abs(ax.y) > 0.9:                        # upright: the axis moves, the section stays round
                    for i in range(n0, n1):
                        co = bm.verts[i].co
                        bm.verts[i].co = Vector((Wa.x + (co.x - a.x), fy(co.y), Wa.z + (co.z - a.z))); cov[i] = 1
                else:                                       # a rod: warp its ends, keep its section
                    Wb = W(a + ax * h)
                    for i in range(n0, n1):
                        co = bm.verts[i].co
                        t = (co - a).dot(ax) / h if h > 1e-6 else 0.0
                        radial = (co - a) - ax * (t * h)
                        bm.verts[i].co = Wa + (Wb - Wa) * t + radial; cov[i] = 1
    for m, bm in setkit.BM.items():
        cov = covered[m]
        for i, v in enumerate(bm.verts):
            if i >= len(cov) or not cov[i]:
                v.co = W(v.co)
    STATE['done'] = True

    def warp_list(lst, box=False):
        for i, p in enumerate(lst):
            if (id(lst), i) in DONE: continue
            if box:
                a, b = W(p[:3]), W(p[3:])
                lst[i] = [min(a.x, b.x), min(a.y, b.y), min(a.z, b.z), max(a.x, b.x), max(a.y, b.y), max(a.z, b.z)]
            else:
                lst[i] = list(W(p))
    warp_list(K.LAMPS); warp_list(K.COLLIDERS, box=True)
    if G is not None: warp_list(G.BIG_LAMPS)
    for r in K.ROOMS:
        r['x0'], r['x1'] = sorted((fx(r['x0']), fx(r['x1'])))
        r['z0'], r['z1'] = sorted((fz(r['z0']), fz(r['z1'])))
        r['y'] = fy(r['y'])
    for it in K.INTERACT + (G.INTERACT if G is not None else []):
        if 'pos' in it: it['pos'] = [round(v, 3) for v in W(it['pos'])]
    K.UPPER_DOORS_ORIG = list(K.UPPER_DOORS)
    K.UPPER_DOORS[:] = [(fx(x), fz(z)) for x, z in K.UPPER_DOORS]
    K.UPPER_WINDOWS[:] = [(fx(x), fz(z)) for x, z in K.UPPER_WINDOWS]
