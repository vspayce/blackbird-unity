# The Mark Hopkins house made grand, after the photographs (Wikimedia Commons; historicalhomesofamerica.com):
# the Grand Hall's two storeys of carved walnut Gothic arcade on clustered posts, the stair rising straight to a
# landing and on up, the organ high in a great pointed arch at the back, gas jets in clusters on every post,
# Gothic tracery wainscot under a picture rail hung salon-style, a painted frieze, a coved ceiling with murals
# in its panels and a ribbed glass skylight; a pale tiled floor with patterned borders and Eastlake benches.
#
# Used by hopkins_grand.py: K is the prototype's build_hopkins module; patch() swaps in grand versions of its
# builders before the house is built; build() adds what's new.
import math
import numpy as np
from mathutils import Vector, Matrix
from setkit import (wbox, lbox, cyl, sphere, tube, frame, canvas, bm_for, image, normal_map, noise, hexrgb, material)

K = None
BIG_LAMPS = []      # lamps that should be strong (the hall's)
INTERACT = []       # new places to use

T = 0.35


# ---- materials -------------------------------------------------------------------------------------------

def tracery_tex(name, base='#2a1a10', size=512):
    """Walnut panelling carved with Gothic tracery: tall panels with pointed heads, a trefoil in each."""
    H = W = size
    yy, xx = np.mgrid[0:H, 0:W] / size
    px = (xx * 3) % 1            # three panels across
    fx = px - 0.5
    fy = yy
    # a pointed-arch panel: a rectangle below 0.55 and two arcs meeting at 0.9
    r = 0.42
    left = np.hypot(px - (0.1 + r), np.clip(fy - 0.55, 0, None)) < r
    right = np.hypot(px - (0.9 - r), np.clip(fy - 0.55, 0, None)) < r
    inside = (px > 0.1) & (px < 0.9) & (fy > 0.06) & (fy < 0.92) & left & right
    edge = inside & ~((px > 0.14) & (px < 0.86) & (fy > 0.1) & (np.hypot(px - (0.1 + r), np.clip(fy - 0.55, 0, None)) < r - 0.04)
                      & (np.hypot(px - (0.9 - r), np.clip(fy - 0.55, 0, None)) < r - 0.04))
    # a trefoil near the head
    a = np.arctan2(fy - 0.7, fx)
    tre = np.hypot(fx, fy - 0.7) < 0.09 + 0.04 * np.cos(3 * (a - math.pi / 2))
    grain = 0.85 + 0.2 * noise(H, W, 3, 21) + 0.08 * np.sin(yy * 260 + noise(H, W, 18, 22) * 6)
    col = hexrgb(base) * grain[..., None]
    col[inside] *= 0.78          # the sunk field
    col[edge] *= 1.35            # the moulded edge catches the light
    col[tre] *= 1.3
    height = np.where(inside, 0.0, 1.0) + np.where(edge, 0.6, 0) + np.where(tre & inside, 0.5, 0)
    return image(name, col), normal_map(name + '_n', height * 3)


def paper_tex(name, ground='#b9ae92', ink='#8a7a58', size=512):
    """The hall's pale stencilled wall: a lozenge diaper with small flowers, as in the photographs."""
    H = W = size
    yy, xx = np.mgrid[0:H, 0:W] / size * 6
    fx, fy = xx % 1 - 0.5, yy % 1 - 0.5
    loz = np.abs(np.abs(fx) + np.abs(fy) - 0.45) < 0.03
    a = np.arctan2(fy, fx); rr = np.hypot(fx, fy)
    flower = rr < 0.1 + 0.05 * np.cos(4 * a)
    aged = 0.88 + 0.16 * noise(H, W, 40, 31)
    col = np.where((loz | flower)[..., None], hexrgb(ink), hexrgb(ground)) * aged[..., None]
    return image(name, col)


def tiles_tex(name, size=1024):
    """Pale encaustic tiles: a plain field with a dark patterned band (the hall floor's borders)."""
    H = W = size
    yy, xx = np.mgrid[0:H, 0:W] / size * 8
    fx, fy = xx % 1, yy % 1
    joint = (fx < 0.02) | (fy < 0.02)
    base = hexrgb('#cfc6b0') * (0.9 + 0.12 * noise(H, W, 6, 41))[..., None]
    col = np.where(joint[..., None], hexrgb('#8a8070'), base)
    return image(name, col), normal_map(name + '_n', np.where(joint, 0.0, 1.0))


def band_tex(name, size=512):
    """The border band: dark red and black tiles in a Greek key, edged with buff."""
    H = W = size
    yy, xx = np.mgrid[0:H, 0:W] / size
    k = (xx * 8) % 1; v = yy
    key = ((v > 0.25) & (v < 0.35)) | ((v > 0.65) & (v < 0.75)) | ((k < 0.12) & (v > 0.25) & (v < 0.75)) | \
          ((k > 0.5) & (k < 0.62) & (v > 0.35) & (v < 0.65))
    edge = (v < 0.12) | (v > 0.88)
    col = np.where(key[..., None], hexrgb('#1a1412'), hexrgb('#6a2418'))
    col = np.where(edge[..., None], hexrgb('#c8b890'), col) * (0.9 + 0.12 * noise(H, W, 8, 43))[..., None]
    return image(name, col)


def materials():
    t, n = tracery_tex('tracery_walnut'); material('tracery_walnut', tex=t, nrm=n, rough=0.45, scale=1.2)
    t, n = tracery_tex('tracery_light', base='#4a3020'); material('tracery_light', tex=t, nrm=n, rough=0.45, scale=1.2)
    material('hall_paper', tex=paper_tex('hall_paper'), rough=0.85, scale=1.6)
    material('frieze_paint', tex=paper_tex('frieze_paint', '#5a3a1e', '#b08a48'), rough=0.8, scale=1.0)
    t, n = tiles_tex('hall_tiles'); material('hall_tiles', tex=t, nrm=n, rough=0.3, scale=3.2)
    material('tile_band', tex=band_tex('tile_band'), rough=0.3, scale=None)
    material('carved_walnut', '#24160e', rough=0.42)
    material('bronze_dark', '#3a2a18', rough=0.35, metal=0.85)
    material('cove_gold', '#8a6a34', rough=0.5, metal=0.6)
    material('cove_ground', '#3a2a1a', rough=0.85)


# ---- patches ---------------------------------------------------------------------------------------------

def patch():
    import hopkins_house as HH
    HH.conservatory = raised_conservatory
    K.build_solarium_glass = palm_house
    orig_rooms = K.rooms

    def rooms():
        R = orig_rooms()
        h = R['hall']
        h['wains'] = 'tracery_walnut'
        h['upper'] = 'hall_paper'
        h['floor'] = 'hall_tiles'
        return R
    K.rooms = rooms
    K.build_hall = grand_hall


# ---- pieces ----------------------------------------------------------------------------------------------

def quad(mat, a, b, c, d, both=True):
    bm = bm_for(mat)
    vs = [bm.verts.new(Vector(p)) for p in (a, b, c, d)]
    bm.faces.new(vs)
    if both:
        vs2 = [bm.verts.new(Vector(p)) for p in (d, c, b, a)]
        bm.faces.new(vs2)


def pointed(u0, u1, v_spring, n=10, sharp=0.8):
    """Points along a pointed (Gothic) arch from u0 to u1 springing at v_spring. sharp: radius / span."""
    span = u1 - u0
    r = span * sharp
    cl, cr = u0 + r, u1 - r
    mid = (u0 + u1) / 2
    apex = math.sqrt(max(0.0, r * r - (mid - cl) ** 2))
    pts = []
    a_end = math.acos(max(-1.0, min(1.0, (mid - cl) / r)))     # the left arc, centred right of it, meets the apex here
    for k in range(n + 1):        # left half: from the springing (angle pi) up to the apex
        a = math.pi - (math.pi - a_end) * k / n
        pts.append((cl + r * math.cos(a), v_spring + r * math.sin(a)))
    b_end = math.acos(max(-1.0, min(1.0, (mid - cr) / r)))
    for k in range(1, n + 1):     # right half: from the apex down to the springing (angle 0)
        a = b_end * (1 - k / n)
        pts.append((cr + r * math.cos(a), v_spring + r * math.sin(a)))
    return pts, v_spring + apex


def arch(F, u0, u1, v_spring, v_top, depth=0.3, mat='carved_walnut', spandrel='tracery_walnut', w0=0.0, sharp=0.7):
    """A pointed arch between two posts in frame F (u along the arcade, w across it), with its spandrels
    filled with carved panelling up to v_top and a quatrefoil boss in each."""
    pts, apex = pointed(u0, u1, v_spring, sharp=sharp)
    # the arch moulding: a run of short boxes following the curve
    for (ua, va), (ub, vb) in zip(pts, pts[1:]):
        c = Vector(((ua + ub) / 2, (va + vb) / 2, w0))
        d = Vector((ub - ua, vb - va, 0))
        ang = math.atan2(d.y, d.x)
        M = F @ Matrix.Translation(c) @ Matrix.Rotation(ang, 4, 'Z')
        lbox(M, mat, -d.length / 2 - 0.01, d.length / 2 + 0.01, -0.09, 0.09, -depth / 2, depth / 2)
    # spandrels: fill between the curve and the beam above, front and back faces
    for (ua, va), (ub, vb) in zip(pts, pts[1:]):
        for w in (w0 - depth / 2 + 0.03, w0 + depth / 2 - 0.03):
            quad(spandrel, F @ Vector((ua, va + 0.05, w)), F @ Vector((ub, vb + 0.05, w)),
                 F @ Vector((ub, v_top, w)), F @ Vector((ua, v_top, w)))
    # a quatrefoil boss in each spandrel
    for uq in (u0 + (u1 - u0) * 0.16, u1 - (u1 - u0) * 0.16):
        vq = (v_top + v_spring + (apex - v_spring) * 0.55) / 2
        for w in (w0 - depth / 2, w0 + depth / 2):
            for k in range(4):
                a = k * math.pi / 2
                sphere('gilt_frame', F @ Vector((uq + 0.09 * math.cos(a), vq + 0.09 * math.sin(a), w)), 0.075, 8)
    return apex


def clustered_post(x, z, y0, y1, r=0.16, lamp=None, facing=(0, 0, 1), mat='carved_walnut', band='gilt_frame'):
    """A carved walnut pier: a core and four colonnettes, a moulded base and a gilt-banded capital. lamp: put a
    cluster of three gas globes on a bracket on the side facing `facing` at this height."""
    cyl(mat, (x, y0, z), 0.45, r * 2.4, r * 2.1, seg=8)
    cyl(mat, (x, y0 + 0.45, z), y1 - y0 - 0.95, r, seg=12)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        cyl(mat, (x + 1.15 * r * math.cos(a), y0 + 0.45, z + 1.15 * r * math.sin(a)), y1 - y0 - 0.95, r * 0.42, seg=8)
    cyl(band, (x, y1 - 0.5, z), 0.08, r * 1.9, seg=12)
    cyl(mat, (x, y1 - 0.42, z), 0.42, r * 1.9, r * 2.6, seg=8)
    K.collide(x - r * 1.6, y0, z - r * 1.6, x + r * 1.6, y1, z + r * 1.6)
    if lamp is not None:
        f = Vector(facing).normalized()
        base = Vector((x, lamp, z)) + f * (r * 1.6)
        tip = base + f * 0.45
        tube('brass', [base, base + f * 0.25 + Vector((0, 0.12, 0)), tip], 0.025)
        side = Vector((-f.z, 0, f.x))
        for p in (tip + Vector((0, 0.32, 0)), tip + side * 0.2 + Vector((0, 0.14, 0)), tip - side * 0.2 + Vector((0, 0.14, 0))):
            tube('brass', [tip, p - Vector((0, 0.08, 0))], 0.015)
            cyl('brass', p - Vector((0, 0.1, 0)), 0.06, 0.035, 0.05, seg=8)
            sphere('lamp_glass', p + Vector((0, 0.06, 0)), 0.1, 10)
        K.LAMPS.append(list(tip + Vector((0, 0.25, 0))))


def balustrade_panel(F, u0, u1, v, h=1.0, w=0.0, mat='tracery_light'):
    """A carved gallery front: a solid traceried panel between a plinth and a handrail."""
    lbox(F, 'carved_walnut', u0, u1, v, v + 0.12, w - 0.09, w + 0.09)
    lbox(F, mat, u0, u1, v + 0.12, v + h - 0.12, w - 0.05, w + 0.05)
    lbox(F, 'carved_walnut', u0 - 0.02, u1 + 0.02, v + h - 0.12, v + h, w - 0.1, w + 0.1)


def grand_gasolier(x, y_ceiling, z, drop=4.2):
    """A three-tier brass gasolier: a corona of globes, a ring of arms, and a pendant of smaller lights."""
    y = y_ceiling - drop
    tube('brass', [(x, y_ceiling, z), (x, y + 0.6, z)], 0.035)
    for k in range(6):  # the canopy chains
        a = k * math.pi / 3
        tube('brass', [(x, y_ceiling - 0.05, z), (x + 0.25 * math.cos(a), y_ceiling - 0.6, z + 0.25 * math.sin(a))], 0.01)
    sphere('brass', (x, y + 0.5, z), 0.2, 12)
    cyl('brass', (x, y - 0.4, z), 0.9, 0.08, 0.14, seg=10)
    for tier, (r, dy, n) in enumerate(((1.45, 0.0, 16), (1.0, 0.75, 10), (0.55, -0.45, 6))):
        yt = y + dy
        for k in range(n):
            a = 2 * math.pi * k / n + tier * 0.2
            p = Vector((x + r * math.cos(a), yt + 0.12, z + r * math.sin(a)))
            tube('brass', [(x, yt - 0.05, z), (x + r * 0.55 * math.cos(a), yt - 0.18, z + r * 0.55 * math.sin(a)), p], 0.014)
            cyl('brass', p - Vector((0, 0.04, 0)), 0.05, 0.03, 0.045, seg=8)
            sphere('lamp_glass', p + Vector((0, 0.09, 0)), 0.085 if tier < 2 else 0.065, 10)
        # a gilt ring binding each tier's arms
        for k in range(24):
            a0, a1 = 2 * math.pi * k / 24, 2 * math.pi * (k + 1) / 24
            tube('gilt_frame', [(x + r * 0.55 * math.cos(a0), yt - 0.18, z + r * 0.55 * math.sin(a0)),
                                (x + r * 0.55 * math.cos(a1), yt - 0.18, z + r * 0.55 * math.sin(a1))], 0.012, 5)
    sphere('lamp_glass', (x, y - 0.55, z), 0.14, 12)
    K.LAMPS.append([x, y, z])
    BIG_LAMPS.append([x, y, z])


def torchere_figure(x, z, rot, h=2.0):
    """A bronze figure on a marble pedestal holding up a cluster of gas globes: the stair's newel lights."""
    K.plinth(x, z, 1.1, 0.6, 'marble_white')
    F = Matrix.Translation((x, 1.16, z)) @ Matrix.Rotation(rot, 4, 'Y')
    k = h / 2.0
    cyl('bronze_dark', F @ Vector((0, 0, 0)), 0.15, 0.3 * k, 0.26 * k, seg=12)                      # rock
    cyl('bronze_dark', F @ Vector((0, 0.15 * k, 0)), 0.8 * k, 0.2 * k, 0.15 * k, seg=12)           # draped figure
    cyl('bronze_dark', F @ Vector((0, 0.95 * k, 0)), 0.5 * k, 0.15 * k, 0.17 * k, seg=12)          # torso
    sphere('bronze_dark', F @ Vector((0, 1.56 * k, 0.02)), 0.11 * k, 12)
    cyl('bronze_dark', F @ Vector((0, 1.45 * k, 0)), 0.1 * k, 0.05 * k, seg=8)
    hand = F @ Vector((0.05 * k, 2.15 * k, 0))
    tube('bronze_dark', [F @ Vector((0.15 * k, 1.38 * k, 0)), F @ Vector((0.22 * k, 1.8 * k, 0.02)), hand], 0.045 * k, 8)
    tube('bronze_dark', [F @ Vector((-0.15 * k, 1.38 * k, 0)), F @ Vector((-0.24 * k, 1.1 * k, 0.12 * k)), F @ Vector((-0.18 * k, 0.9 * k, 0.2 * k))], 0.045 * k, 8)
    top = Vector(hand) + Vector((0, 0.1, 0))
    for a in range(5):
        ang = a * 2 * math.pi / 5
        p = top + Vector((0.32 * math.cos(ang), 0.18, 0.32 * math.sin(ang)))
        tube('bronze_dark', [top, p], 0.018)
        sphere('lamp_glass', p + Vector((0, 0.1, 0)), 0.11, 10)
    sphere('lamp_glass', top + Vector((0, 0.42, 0)), 0.13, 10)
    K.LAMPS.append(list(top + Vector((0, 0.3, 0))))
    K.collide(x - 0.35, 0, z - 0.35, x + 0.35, 3.5, z + 0.35)


def eastlake_bench(x, z, rot):
    """A pair of benches back to back with a shared carved back, as in the photographs."""
    F = Matrix.Translation((x, 0, z)) @ Matrix.Rotation(rot, 4, 'Y')
    L = 3.2
    for s in (-1, 1):
        lbox(F, 'velvet_red', -L / 2, L / 2, 0.44, 0.52, s * 0.08, s * 0.55)
        lbox(F, 'carved_walnut', -L / 2, L / 2, 0.36, 0.44, s * 0.08, s * 0.58)
        for u in (-L / 2 + 0.08, 0, L / 2 - 0.08):
            lbox(F, 'carved_walnut', u - 0.05, u + 0.05, 0, 0.36, s * 0.5, s * 0.58)
        lbox(F, 'carved_walnut', -L / 2, L / 2, 0.12, 0.17, s * 0.3, s * 0.36)    # stretcher
    lbox(F, 'tracery_walnut', -L / 2, L / 2, 0.52, 1.0, -0.06, 0.06)
    lbox(F, 'carved_walnut', -L / 2 - 0.05, L / 2 + 0.05, 1.0, 1.06, -0.09, 0.09)
    for u in (-L / 2, L / 2):  # end posts with arms
        lbox(F, 'carved_walnut', u - 0.06, u + 0.06, 0, 1.1, -0.6, 0.6)
    c = F @ Vector((0, 0, 0))
    K.collide(c.x - 1.7, 0, c.z - 0.65, c.x + 1.7, 1.0, c.z + 0.65) if abs(math.sin(rot)) < 0.5 else \
        K.collide(c.x - 0.65, 0, c.z - 1.7, c.x + 0.65, 1.0, c.z + 1.7)


# ---- the Grand Hall -----------------------------------------------------------------------------------------

def grand_hall(R):
    x0, x1, z0, z1 = -7, 7, -6, 10
    gy = 6.0                      # the gallery floor
    top = 12.0                    # the top of the upper arcade; the cove springs here
    HH = K.HALL_H                 # 14: the cove's crown
    zf, zl, zb = -0.1, -4.6, -6.0
    xl, xs = 3.2, 5.6
    collide = K.collide

    # --- the stair: one wide flight to a landing, then a flight up each side to the gallery (as before, so the
    # walking is unchanged), now in marble with a red runner and carved balustrades ---
    n_main, n_side = 15, 8
    rise = (gy / 2) / n_main
    for k in range(n_main):
        wbox('marble_white', -2.4, 2.4, 0, (k + 1) * rise, zf - (k + 1) * 0.3, zf - k * 0.3)
        wbox('velvet_red', -1.5, 1.5, (k + 1) * rise, (k + 1) * rise + 0.008, zf - (k + 1) * 0.3, zf - k * 0.3 + 0.01)
        tube('brass', [(-1.5, k * rise + rise + 0.02, zf - k * 0.3 - 0.02), (1.5, k * rise + rise + 0.02, zf - k * 0.3 - 0.02)], 0.008, 5)
    wbox('marble_white', -xl, xl, gy / 2 - 0.3, gy / 2, zb, zl)
    wbox('velvet_red', -2.6, 2.6, gy / 2, gy / 2 + 0.008, zb + 0.2, zl - 0.1)
    for s_ in (-1, 1):
        for k in range(n_side):
            xa, xb = s_ * (xl + k * 0.3), s_ * (xl + (k + 1) * 0.3)
            ytop = gy / 2 + (k + 1) * (gy / 2) / n_side
            wbox('marble_white', min(xa, xb), max(xa, xb), ytop - 0.45, ytop, zb, zl)
            wbox('velvet_red', min(xa, xb), max(xa, xb), ytop, ytop + 0.008, zb + 0.25, zl - 0.25)
        wbox('carved_walnut', min(s_ * xs, s_ * 7), max(s_ * xs, s_ * 7), gy - 0.4, gy, zb, zl)
        wbox('parquet', min(s_ * xs, s_ * 7), max(s_ * xs, s_ * 7), gy, gy + 0.02, zb, zl)
        # the main flight's carved balustrades, rising with it, and newels with bronze torchere figures
        Fm = frame((s_ * 2.5, 0, zf), (0, 0, -1), (s_, 0, 0))
        n = 9
        for i in range(n):
            ua, ub = (zf - zf) + 4.5 * i / n, 4.5 * (i + 1) / n
            va, vb = gy / 2 * i / n, gy / 2 * (i + 1) / n
            balustrade_panel(Fm, ua, ub, (va + vb) / 2, 0.95)
        tube('carved_walnut', [(s_ * 2.5, 1.0, zf), (s_ * 2.5, gy / 2 + 1.0, zl)], 0.06)
        torchere_figure(s_ * 2.55, zf + 0.75, math.pi if s_ < 0 else 0)
        collide(min(s_ * 2.4, s_ * 2.6), 0, zl, max(s_ * 2.4, s_ * 2.6), gy / 2 + 1.0, zf)
        collide(min(s_ * xl, s_ * 7), 0, zb, max(s_ * xl, s_ * 7), gy / 2 - 0.2, zl)
        # the side flights' open side (toward the hall), railed
        Fs = frame((s_ * xl, 0, zl), (s_, 0, 0), (0, 0, 1))
        for i in range(8):
            ua, ub = 2.4 * i / 8, 2.4 * (i + 1) / 8
            vv = gy / 2 + (gy / 2) * (i + 0.5) / 8
            balustrade_panel(Fs, ua, ub, vv - 0.45, 0.95)
        collide(min(s_ * 2.4, s_ * 4.6), gy / 2, zl - 0.05, max(s_ * 2.4, s_ * 4.6), gy - 0.3, zl + 0.1)
    K.STAIRS.append([-2.4, 2.4, zl, zf, 'z', gy / 2, 0.0])
    K.STAIRS.append([-xl, xl, zb, zl, 'z', gy / 2, gy / 2])
    K.STAIRS.append([-xs, -xl, zb, zl, 'x', gy, gy / 2])
    K.STAIRS.append([xl, xs, zb, zl, 'x', gy / 2, gy])

    # --- the gallery: round all four sides now. Along the sides and front as before, and a bridge across the back
    # at gallery level (over the stair's head) carrying the organ loft ---
    zbridge0, zbridge1 = zl, zl + 0.8
    strips = ((x0, x1, z1 - 2, z1), (x0, x0 + 2, zl, z1), (x1 - 2, x1, zl, z1), (x0, x1, zbridge0, zbridge1), (-xl, xl, zb, zl))
    for (a0, a1, b0, b1) in strips:
        wbox('carved_walnut', a0, a1, gy - 0.4, gy, b0, b1)
        wbox('parquet', a0, a1, gy, gy + 0.02, b0, b1)
    K.LEVELS.append(dict(y=gy, rects=[list(s) for s in strips] + [[-7, -xs, zb, zl], [xs, 7, zb, zl]]))

    # --- the arcade: clustered posts carrying pointed arches, on the ground and gallery storeys ---
    front_posts = [(-5, 8), (-1.7, 8), (1.7, 8), (5, 8)]
    side_posts = [(s * 5, z) for s in (-1, 1) for z in (5.5, 1.5, -2.5)]
    back_posts = [(-5, zbridge1), (5, zbridge1)]       # the stair climbs between them
    for (x, z) in front_posts + side_posts + back_posts:
        inward = Vector((-x, 0, 2 - z)).normalized()
        clustered_post(x, z, 0, gy - 0.4, lamp=2.7 if (x, z) not in back_posts else None, facing=inward)
        clustered_post(x, z, gy, top, r=0.14, lamp=8.6 if (x, z) in front_posts or (x, z) in side_posts else None, facing=inward)

    def arcade(points, F_of, v_spring, v_top, depth):
        for (a, b) in zip(points, points[1:]):
            F = F_of(a, b)
            L = (Vector((b[0], 0, b[1])) - Vector((a[0], 0, a[1]))).length
            arch(F, 0.22, L - 0.22, v_spring, v_top, depth=depth)

    def F_between(a, b):
        A, B = Vector((a[0], 0, a[1])), Vector((b[0], 0, b[1]))
        r = (B - A).normalized()
        return frame(A, r, Vector((-r.z, 0, r.x)))

    rows = [front_posts,
            [(-5, 8), (-5, 5.5), (-5, 1.5), (-5, -2.5), (-5, zbridge1)],
            [(5, 8), (5, 5.5), (5, 1.5), (5, -2.5), (5, zbridge1)],
            back_posts]
    for row in rows:
        if row is not back_posts:
            arcade(row, F_between, 2.8, gy - 0.4, 0.34)      # ground storey: under the gallery's edge
            arcade(row, F_between, 9.4, top, 0.28)           # gallery storey
        else:
            # across the back the bridge spans the stair on great carved brackets
            F = F_between(*row)
            for u, sgn in ((0.0, 1), (10.0, -1)):
                for k in range(6):
                    lbox(F, 'carved_walnut', u + sgn * (0.1 + k * 0.22) - 0.12, u + sgn * (0.1 + k * 0.22) + 0.12, gy - 0.4 - (1.6 - k * 0.27), gy - 0.4, -0.16, 0.16)
        # the gallery front between the posts (the back row's is the loft's)
        for (a, b) in zip(row, row[1:]):
            F = F_between(a, b)
            L = (Vector((b[0], 0, b[1])) - Vector((a[0], 0, a[1]))).length
            balustrade_panel(F, 0.2, L - 0.2, gy, 1.05)
        # the beams the arches carry
        for (a, b) in zip(row, row[1:]):
            F = F_between(a, b)
            L = (Vector((b[0], 0, b[1])) - Vector((a[0], 0, a[1]))).length
            lbox(F, 'carved_walnut', 0, L, gy - 0.4, gy, -0.2, 0.2)
            lbox(F, 'gilt_frame', 0, L, gy - 0.46, gy - 0.4, -0.21, 0.21)
            lbox(F, 'carved_walnut', 0, L, top - 0.1, top + 0.25, -0.18, 0.18)
    # gallery fronts are railed for walking
    collide(x0 + 1.95, gy, z1 - 2.05, x1 - 1.95, gy + 1.05, z1 - 1.95)
    for xx in (x0 + 2, x1 - 2):
        collide(xx - 0.06, gy, zbridge1, xx + 0.06, gy + 1.05, z1 - 2)
    collide(-5, gy, zbridge1 - 0.05, 5, gy + 1.05, zbridge1 + 0.05)

    # --- the organ, high at the back in a great pointed arch over the loft ---
    Fo = frame((-4.2, 0, zb + T / 2 + 0.02), (1, 0, 0), (0, 0, 1))
    K.organ(frame((-3.0, gy, zb + T / 2 + 0.05), (1, 0, 0), (0, 0, 1)), 0, 6.0, 7.2)
    pts, apex = pointed(0, 8.4, 8.8, n=14, sharp=0.55)
    for (ua, va), (ub, vb) in zip(pts, pts[1:]):
        c = Vector(((ua + ub) / 2, (va + vb) / 2, 0.15)); d = Vector((ub - ua, vb - va, 0))
        M = Fo @ Matrix.Translation(c) @ Matrix.Rotation(math.atan2(d.y, d.x), 4, 'Z')
        lbox(M, 'carved_walnut', -d.length / 2 - 0.02, d.length / 2 + 0.02, -0.18, 0.18, -0.15, 0.15)
        lbox(M, 'gilt_frame', -d.length / 2, d.length / 2, -0.21, -0.18, -0.12, 0.12)
    for u in (0.0, 8.4):
        lbox(Fo, 'carved_walnut', u - 0.25, u + 0.25, gy, 8.8, 0, 0.35)
        sphere('gilt_frame', Fo @ Vector((u, 8.95, 0.18)), 0.16, 10)
    pinnacle_top = Fo @ Vector((4.2, apex + 0.2, 0.15))
    K.pinnacle(pinnacle_top.x, pinnacle_top.y, pinnacle_top.z, h=1.0, r=0.15)
    INTERACT.append(dict(id='organ', label='The great organ', pos=[0.0, gy + 1.0, zb + 1.6], r=2.2))

    # --- the walls: tracery wainscot (from the room's lining), pale stencilled paper, a picture rail, a painted
    # frieze; the gallery storey's walls the same; and the pictures salon-hung frame to frame ---
    centre = Vector(((x0 + x1) / 2, 0, (z0 + z1) / 2))
    walls = (((x0, z0), (x0, z1)), ((x1, z1), (x1, z0)), ((x0, z1), (x1, z1)))
    for (a, b) in walls:
        A, B = Vector((a[0], 0, a[1])), Vector((b[0], 0, b[1]))
        rr = (B - A).normalized(); nn = Vector((-rr.z, 0, rr.x))
        if nn.dot(centre - A) < 0: nn = -nn
        F = frame(A, rr, nn) @ Matrix.Translation((0, 0, T / 2 + 0.01))
        L = (B - A).length
        cuts = sorted((frame(A, rr, nn).inverted() @ Vector((dx, 0, dz))).x for dx, dz in K.UPPER_DOORS
                      if abs((frame(A, rr, nn).inverted() @ Vector((dx, 0, dz))).z) < 0.3 and 0 < (frame(A, rr, nn).inverted() @ Vector((dx, 0, dz))).x < L)
        # picture rail and frieze on the ground storey (under the gallery)
        lbox(F, 'carved_walnut', 0, L, 4.0, 4.12, 0, 0.08)
        lbox(F, 'frieze_paint', 0, L, 4.12, 5.4, 0, 0.012)
        lbox(F, 'carved_walnut', 0, L, 5.4, 5.6, 0, 0.14)
        # the gallery storey's walls: wainscot, paper, frieze, round the doorways
        u = 0.0
        for c in cuts + [L + 10]:
            ue = min(L, c - K.D / 2 - 0.15)
            if ue > u:
                lbox(F, 'tracery_walnut', u, ue, gy, gy + 1.2, 0, 0.03)
                lbox(F, 'hall_paper', u, ue, gy + 1.2, top - 1.0, 0, 0.012)
            if c > L: break
            lbox(F, 'hall_paper', c - K.D / 2 - 0.15, c + K.D / 2 + 0.15, gy + K.DH + 0.15, top - 1.0, 0, 0.012)
            for side in (-1, 1):
                lbox(F, 'carved_walnut', c + side * K.D / 2 - 0.15 * (side < 0), c + side * K.D / 2 + 0.15 * (side > 0), gy, gy + K.DH + 0.15, 0, 0.06)
            pa, _ = pointed(c - K.D / 2 - 0.15, c + K.D / 2 + 0.15, gy + K.DH, n=6)
            for (ua, va), (ub, vb) in zip(pa, pa[1:]):
                cc = Vector(((ua + ub) / 2, (va + vb) / 2, 0.03)); d = Vector((ub - ua, vb - va, 0))
                lbox(F @ Matrix.Translation(cc) @ Matrix.Rotation(math.atan2(d.y, d.x), 4, 'Z'), 'carved_walnut', -d.length / 2, d.length / 2, -0.07, 0.07, -0.03, 0.03)
            u = c + K.D / 2 + 0.15
        lbox(F, 'carved_walnut', 0, L, top - 1.0, top - 0.88, 0, 0.1)
        lbox(F, 'frieze_paint', 0, L, top - 0.88, top, 0, 0.012)
    # the pictures: below the rail on the ground storey, two rows on the gallery storey
    idx = 0
    hang = [(frame((x0 + T / 2 + 0.03, 0, z1), (0, 0, -1), (1, 0, 0)), 16), (frame((x1 - T / 2 - 0.03, 0, z0), (0, 0, 1), (-1, 0, 0)), 16)]
    for F, L in hang:
        for u in (1.6, 3.2, 6.0, 7.8, 9.6, 12.4, 14.4):
            if abs(u - 4) < 1.2 or abs(u - 12) < 1.2: continue      # the ground-floor doors
            K.frame_painting(F, u, 1.6, 1.5, 1.1, idx); idx += 1
            K.frame_painting(F, u, 2.85, 1.2, 0.9, idx + 7); idx += 1
        for u in (4.5, 6.6, 8.7, 10.8):
            K.frame_painting(F, u, gy + 1.45, 1.8, 1.25, idx); idx += 1
            K.frame_painting(F, u, gy + 2.95, 1.2, 0.85, idx + 5); idx += 1
    Ff = frame((x0, 0, z1 - T / 2 - 0.03), (1, 0, 0), (0, 0, -1))
    for u in (2.6, 11.4):
        K.frame_painting(Ff, u, gy + 1.4, 2.6, 1.9, idx); idx += 1
    for u in (1.6, 12.4):
        K.frame_painting(Ff, u, 1.5, 1.6, 1.2, idx); idx += 1

    # --- the ceiling: a cove from the arcade's top up to a flat crown, painted panels between gilt ribs, and the
    # skylight: a ribbed laylight of stained glass in a deep carved frame ---
    inset = 2.0
    n = 8
    prof = [(inset * (1 - math.cos(t * math.pi / 2)), top + 0.25 + (HH - top - 0.25) * math.sin(t * math.pi / 2)) for t in [i / n for i in range(n + 1)]]
    sides = [((x0, z0), (x0, z1), (1, 0)), ((x1, z1), (x1, z0), (-1, 0)), ((x0, z1), (x1, z1), (0, -1)), ((x1, z0), (x0, z0), (0, 1))]
    mural = 0
    for (a, b, inw) in sides:
        A = Vector((a[0], 0, a[1])); B = Vector((b[0], 0, b[1]))
        I = Vector((inw[0], 0, inw[1]))
        dir_ = (B - A).normalized()
        L = (B - A).length
        for (da, ya), (db, yb) in zip(prof, prof[1:]):
            # each band shrinks toward the middle at the corners (mitred)
            pa0 = A + I * da + dir_ * da; pa1 = B + I * da - dir_ * da
            pb0 = A + I * db + dir_ * db; pb1 = B + I * db - dir_ * db
            quad('cove_ground', (pa0.x, ya, pa0.z), (pa1.x, ya, pa1.z), (pb1.x, yb, pb1.z), (pb0.x, yb, pb0.z))
        # painted panels on the cove: the Association's pictures as murals, each over a strip of the curve
        panels = max(2, int(L // 4))
        for p in range(panels):
            t0, t1 = (p + 0.12) / panels, (p + 0.88) / panels
            mat, _ = K.PICTURES[(mural * 7 + 3) % len(K.PICTURES)]; mural += 1
            for (da, ya), (db, yb), k in zip(prof[1:-2], prof[2:-1], range(n)):
                off = 0.02
                q = []
                for (d_, y_) in ((da, ya), (db, yb)):
                    s0 = A + I * (d_ - off) + dir_ * (d_ + (L - 2 * d_) * t0)
                    s1 = A + I * (d_ - off) + dir_ * (d_ + (L - 2 * d_) * t1)
                    q.append((s0, s1, y_))
                (s0a, s1a, ya_), (s0b, s1b, yb_) = q
                v0, v1 = k / (n - 2), (k + 1) / (n - 2)
                canvas(mat, [(s0a.x, ya_, s0a.z), (s0b.x, yb_, s0b.z), (s1b.x, yb_, s1b.z), (s1a.x, ya_, s1a.z)],
                       uv=((1, v0), (1, v1), (0, v1), (0, v0)))
            # gilt ribs between panels
            for t in (p / panels, (p + 1) / panels):
                pts = [A + I * (d_ - 0.05) + dir_ * (d_ + (L - 2 * d_) * t) for (d_, _) in prof]
                tube('cove_gold', [(q_.x, y_, q_.z) for q_, (_, y_) in zip(pts, prof)], 0.06, 6)
    # the crown: a flat ring round the skylight's well
    cx0, cx1, cz0, cz1 = x0 + inset, x1 - inset, z0 + inset, z1 - inset
    sx0, sx1, sz0, sz1 = -3.6, 3.6, -1.4, 7.4
    for (a0, a1, b0, b1) in ((cx0, cx1, cz0, sz0), (cx0, cx1, sz1, cz1), (cx0, sx0, sz0, sz1), (sx1, cx1, sz0, sz1)):
        wbox('cove_ground', a0, a1, HH, HH + 0.1, b0, b1)
    wbox('walnut_panel', x0, x1, HH + 0.9, HH + 1.1, z0, z1)        # the roof deck above, unseen
    # the well: carved sides, then the laylight
    for (a0, a1, b0, b1) in ((sx0, sx1, sz0 - 0.2, sz0), (sx0, sx1, sz1, sz1 + 0.2), (sx0 - 0.2, sx0, sz0, sz1), (sx1, sx1 + 0.2, sz0, sz1)):
        wbox('tracery_walnut', a0, a1, HH, HH + 0.75, b0, b1)
        wbox('gilt_frame', a0, a1, HH - 0.08, HH, b0, b1)
    wbox('stained', sx0, sx1, HH + 0.72, HH + 0.75, sz0, sz1)
    for xx in np.linspace(sx0, sx1, 7):
        wbox('carved_walnut', xx - 0.05, xx + 0.05, HH + 0.62, HH + 0.72, sz0, sz1)
    for zz in np.linspace(sz0, sz1, 9):
        wbox('carved_walnut', sx0, sx1, HH + 0.62, HH + 0.72, zz - 0.05, zz + 0.05)
    # the frieze of the crown: gilt
    for (a0, a1, b0, b1) in ((cx0, cx1, cz0 - 0.1, cz0), (cx0, cx1, cz1, cz1 + 0.1), (cx0 - 0.1, cx0, cz0, cz1), (cx1, cx1 + 0.1, cz0, cz1)):
        wbox('gilt_frame', a0, a1, HH - 0.15, HH + 0.02, b0, b1)

    # --- light: two great tiered gasoliers in the well's length ---
    grand_gasolier(0, HH, 5.6, drop=4.6)
    grand_gasolier(0, HH, 0.4, drop=4.6)

    # --- the floor: pale tiles (the room's floor), with patterned bands framing the walks, as in the photographs ---
    def band(a0, a1, b0, b1, along_x):
        corners = [(a0, 0.004, b0), (a0, 0.004, b1), (a1, 0.004, b1), (a1, 0.004, b0)]   # facing up
        L = (a1 - a0) if along_x else (b1 - b0)
        reps = max(1, round(L / 0.6))
        uv = ((0, 0), (0, 1), (reps, 1), (reps, 0)) if along_x else ((0, 0), (reps, 0), (reps, 1), (0, 1))
        canvas('tile_band', corners, uv=uv)
    for x in (-4.6, 4.2):
        band(x, x + 0.4, zf + 0.3, z1 - 0.2, False)
    band(-4.6, 4.6, z1 - 0.6, z1 - 0.2, True)
    band(-4.6, 4.6, zf + 0.3, zf + 0.7, True)
    for x in (-6.7, 6.3):
        band(x, x + 0.4, zl, z1 - 0.2, False)

    # --- Eastlake benches back to back down the middle, as in the photographs ---
    eastlake_bench(0, 6.4, math.pi / 2)
    eastlake_bench(0, 2.6, math.pi / 2)


# ---- everything new beyond the patched builders --------------------------------------------------------------

def build(R):
    rear_verandas()
    terraces()


# ---- outside --------------------------------------------------------------------------------------------------

def ngon(mat, pts, both=True):
    bm = bm_for(mat)
    bm.faces.new([bm.verts.new(Vector(p)) for p in pts])
    if both:
        bm.faces.new([bm.verts.new(Vector(p)) for p in reversed(pts)])


def wall_run(a, b, y0, y1, glass='glass_cons', plinth=0.6, mullion=1.5, door=None):
    """A glazed wall from a to b (x, z): a stone plinth, glass in iron mullions with a transom, a stone cap.
    door: (u0, u1) along the run left open (a pair of glazed doors stand back in it)."""
    A, B = Vector((a[0], 0, a[1])), Vector((b[0], 0, b[1]))
    r = (B - A).normalized(); L = (B - A).length
    F = frame(A, r, Vector((-r.z, 0, r.x)))
    spans = [(0, L)] if not door else [(0, door[0]), (door[1], L)]
    for u0, u1 in spans:
        if u1 - u0 < 0.05: continue
        lbox(F, 'ashlar', u0, u1, y0 - 0.1, y0 + plinth, -0.25, 0.25)
        lbox(F, 'trim_stone', u0, u1, y0 + plinth, y0 + plinth + 0.08, -0.3, 0.3)
        lbox(F, glass, u0, u1, y0 + plinth + 0.08, y1, -0.015, 0.015)
        n = max(1, round((u1 - u0) / mullion))
        for k in range(n + 1):
            u = u0 + (u1 - u0) * k / n
            lbox(F, 'iron', u - 0.045, u + 0.045, y0 + plinth, y1, -0.06, 0.06)
        lbox(F, 'iron', u0, u1, y0 + 2.8, y0 + 2.86, -0.05, 0.05)
        K.collide(min(A.x + r.x * u0, A.x + r.x * u1) - 0.25 * abs(r.z), y0, min(A.z + r.z * u0, A.z + r.z * u1) - 0.25 * abs(r.x),
                  max(A.x + r.x * u0, A.x + r.x * u1) + 0.25 * abs(r.z), y1, max(A.z + r.z * u0, A.z + r.z * u1) + 0.25 * abs(r.x))
    lbox(F, 'trim_stone', -0.1, L + 0.1, y1, y1 + 0.25, -0.3, 0.3)
    if door:
        u0, u1 = door
        for s in (u0, u1):
            lbox(F, 'trim_stone', s - 0.2, s + 0.2, y0 - 0.1, y1, -0.3, 0.3)
        lbox(F, 'trim_stone', u0, u1, y0 + 3.1, y0 + 3.35, -0.3, 0.3)
        lbox(F, glass, u0, u1, y0 + 3.35, y1, -0.015, 0.015)
        for s, d in ((u0 + 0.1, 1), (u1 - 0.1, -1)):        # the leaves, standing open outward
            G = F @ Matrix.Translation((s, 0, 0)) @ Matrix.Rotation(-d * 1.25, 4, 'Y')
            lbox(G, 'iron', 0, d * (u1 - u0) / 2 - d * 0.1, y0, y0 + 3.1, -0.03, 0.03) if d > 0 else \
                lbox(G, 'iron', d * (u1 - u0) / 2 - d * 0.1, 0, y0, y0 + 3.1, -0.03, 0.03)


def barrel(x0, x1, za, zb, sp, sharp=0.55, along='z', rib=1.6, gables=(True, True), glass='glass_cons'):
    """A pointed glass barrel roof spanning x0..x1 (or z when along='x'), running from za to zb, on iron ribs,
    with traceried glass gables at the ends. Returns its apex height."""
    P, apex = pointed(x0, x1, sp, n=10, sharp=sharp)
    def W(u, v, t):  # profile u at height v, at position t along the run
        return (u, v, t) if along == 'z' else (t, v, u)
    for (ua, va), (ub, vb) in zip(P, P[1:]):
        quad(glass, W(ua, va, za), W(ub, vb, za), W(ub, vb, zb), W(ua, va, zb))
    n = max(1, round(abs(zb - za) / rib))
    for k in range(n + 1):
        t = za + (zb - za) * k / n
        tube('iron', [W(u, v + 0.02, t) for u, v in P], 0.05 if k in (0, n) else 0.035, 4)
    for u, v in P[::4]:
        tube('iron', [W(u, v + 0.03, za), W(u, v + 0.03, zb)], 0.025, 4)
    for t, on in ((za, gables[0]), (zb, gables[1])):
        if not on: continue
        ngon(glass, [W(u, v, t) for u, v in P])
        # tracery: a wheel window and mullions
        mid = (x0 + x1) / 2
        cy = sp + (apex - sp) * 0.45
        R = (x1 - x0) * 0.18
        for ring in (R, R * 0.45):
            tube('iron', [W(mid + ring * math.cos(a), cy + ring * math.sin(a), t) for a in np.linspace(0, 2 * math.pi, 25)], 0.04, 4)
        for a in np.linspace(0, 2 * math.pi, 13)[:-1]:
            tube('iron', [W(mid + R * 0.45 * math.cos(a), cy + R * 0.45 * math.sin(a), t), W(mid + R * math.cos(a), cy + R * math.sin(a), t)], 0.025, 4)
        for u in np.linspace(x0 + (x1 - x0) * 0.12, x1 - (x1 - x0) * 0.12, 6):
            # each mullion up to the arch (but not across the wheel)
            v_top = max(v for uu, v in P if abs(uu - u) < (x1 - x0) / 10) - 0.1
            if abs(u - mid) < R: v_top = cy - R
            tube('iron', [W(u, sp, t), W(u, v_top, t)], 0.03, 4)
    return apex


def dome(xc, zc, y0, r, h, mat='glass_cons', ribs=12, lantern=True):
    """A ribbed dome on a cylindrical drum's top at y0, with a lantern and a needle spire."""
    prof = [(r * math.cos(t * math.pi / 2), y0 + h * math.sin(t * math.pi / 2)) for t in np.linspace(0, 1, 8)]
    for (r0, ya), (r1, yb) in zip(prof, prof[1:]):
        cyl(mat, (xc, ya, zc), yb - ya, max(r0, 0.05), max(r1, 0.05), seg=24, cap=False)
    for k in range(ribs):
        a = k * 2 * math.pi / ribs
        tube('iron', [(xc + math.cos(a) * (rr + 0.04), y, zc + math.sin(a) * (rr + 0.04)) for rr, y in prof], 0.06, 4)
    cyl('trim_stone', (xc, y0 - 0.25, zc), 0.35, r + 0.25, seg=24)
    if lantern:
        top = y0 + h
        cyl('trim_stone', (xc, top - 0.1, zc), 0.25, r * 0.22, seg=12)
        cyl(mat, (xc, top + 0.15, zc), 1.2, r * 0.18, seg=12, cap=False)
        for k in range(8):
            a = k * math.pi / 4
            tube('iron', [(xc + math.cos(a) * r * 0.19, top + 0.15, zc + math.sin(a) * r * 0.19), (xc + math.cos(a) * r * 0.19, top + 1.35, zc + math.sin(a) * r * 0.19)], 0.03, 4)
        cyl('slate', (xc, top + 1.35, zc), 1.6, r * 0.24, 0.02, seg=8, cap=False)
        tube('iron', [(xc, top + 2.9, zc), (xc, top + 4.6, zc)], 0.04, 4)
        sphere('gilt_frame', (xc, top + 3.2, zc), 0.12, 8)


def hanging_lantern(x, y_top, z, drop=1.8):
    tube('iron', [(x, y_top, z), (x, y_top - drop, z)], 0.015, 4)
    cyl('iron', (x, y_top - drop - 0.05, z), 0.08, 0.16, 0.05, seg=8)
    cyl('lamp_glass', (x, y_top - drop - 0.45, z), 0.4, 0.13, 0.18, seg=8)
    cyl('iron', (x, y_top - drop - 0.5, z), 0.06, 0.06, 0.16, seg=8)
    K.LAMPS.append([x, y_top - drop - 0.25, z])


def marble_fountain(x, z):
    """A three-tier marble fountain in a wide basin, water falling from tier to tier."""
    cyl('marble_white', (x, 0, z), 0.6, 3.2, 3.3, seg=32)
    cyl('water', (x, 0.5, z), 0.04, 2.95, seg=32)
    for (y, r, h) in ((0.55, 0.4, 1.3), (1.85, 1.6, 0.18), (2.03, 0.25, 0.9), (2.93, 0.95, 0.14), (3.07, 0.15, 0.7), (3.77, 0.45, 0.1)):
        cyl('marble_white', (x, y, z), h, r, r * 0.92, seg=24)
    for (y, r) in ((2.0, 1.5), (3.04, 0.88)):
        cyl('water', (x, y, z), 0.02, r, seg=24)
        cyl('water', (x, y - 0.6, z), 0.6, r + 0.02, r + 0.06, seg=24, cap=False)   # the falling sheet
    for k in range(4):  # four dolphins spouting into the basin
        a = k * math.pi / 2 + math.pi / 4
        p = Vector((x + math.cos(a) * 0.55, 0.9, z + math.sin(a) * 0.55))
        sphere('bronze_dark', p, 0.18, 8)
        tube('water', [p, p + Vector((math.cos(a) * 1.2, -0.2, math.sin(a) * 1.2)), p + Vector((math.cos(a) * 1.9, -0.45, math.sin(a) * 1.9))], 0.02, 4)
    sphere('gilt_frame', (x, 3.95, z), 0.14, 10)
    K.collide(x - 3.2, 0, z - 3.2, x + 3.2, 0.7, z + 3.2)
    INTERACT.append(dict(id='palmfountain', label='The palm house fountain', pos=[x, 0.8, z + 3.4], r=2.0))


def palm_house():
    """The solarium opened through three pointed arches into a cross-shaped palm house: glass on iron ribs, a
    ribbed dome over the crossing, palms, a fountain, and doors at the far end onto the terraces."""
    H5 = K.H
    x0, x1, zf, zb = -9.0, 9.0, -14.0, -27.5
    tx0, tx1, tz0, tz1 = -13.5, 13.5, -24.0, -17.0      # the transept
    zc = (tz0 + tz1) / 2
    sp = 5.0
    # the house wall: four stone piers and three pointed arches, with a band and the glazed lean-to over the
    # solarium as before
    for (pa, pb) in ((-7.3, -6.4), (-3.2, -2.6), (2.6, 3.2), (6.4, 7.3)):
        wbox('ashlar', pa, pb, -0.1, H5 + 1.0, zf - 0.3, zf + 0.3)
        wbox('trim_stone', pa - 0.05, pb + 0.05, 2.5, 2.65, zf - 0.35, zf + 0.35)
        K.collide(pa, 0, zf - 0.3, pb, H5, zf + 0.3)
    for (a, b) in ((-6.4, -3.2), (-2.6, 2.6), (3.2, 6.4)):
        arch(frame((a, 0, zf), (1, 0, 0), (0, 0, 1)), 0, b - a, 2.65, H5 + 1.0, depth=0.6, mat='trim_stone', spandrel='ashlar', sharp=0.6)
    bm = bm_for('glass_clear')
    q = [(-7, H5 + 1.0, zf), (7, H5 + 1.0, zf), (7, H5 + 2.5, -6), (-7, H5 + 2.5, -6)]
    bm.faces.new([bm.verts.new(p) for p in q])
    for k in range(8):
        x = -7 + k * 2
        tube('iron', [(x, H5 + 1.0, zf), (x, H5 + 2.5, -6)], 0.03, 4)
    # floors
    wbox('hall_tiles', x0, x1, -0.1, 0.0, zb, zf)
    wbox('hall_tiles', tx0, tx1, -0.1, 0.0, tz0, tz1)
    # glazed walls round the cross
    door = (x1 - x0) / 2 - 1.6
    runs = [((x0, zf), (x0, tz1)), ((x0, tz1), (tx0, tz1)), ((tx0, tz1), (tx0, tz0)), ((tx0, tz0), (x0, tz0)),
            ((x0, tz0), (x0, zb)), ((x0, zb), (x1, zb)), ((x1, zb), (x1, tz0)), ((x1, tz0), (tx1, tz0)),
            ((tx1, tz0), (tx1, tz1)), ((tx1, tz1), (x1, tz1)), ((x1, tz1), (x1, zf))]
    for a, b in runs:
        wall_run(a, b, 0.0, sp, door=(door, door + 3.2) if (a, b) == ((x0, zb), (x1, zb)) else None)
    # roofs: the nave barrel along z, the transept barrel along x, and the dome over the crossing on a glass drum
    apex = barrel(x0, x1, zf, zb, sp, sharp=0.55, along='z', gables=(False, True))
    # the gable against the house: stone, filling the barrel's profile
    P, _ = pointed(x0, x1, sp, n=10, sharp=0.55)
    ngon('ashlar', [(u, v, zf + 0.02) for u, v in P])
    barrel(tz0, tz1, tx0, tx1, sp, sharp=0.55, along='x')
    drum_r = 4.2
    cyl('glass_cons', (0, apex - 1.4, zc), 2.4, drum_r, seg=24, cap=False)
    for k in range(16):
        a = k * math.pi / 8
        tube('iron', [(math.cos(a) * drum_r, apex - 1.4, zc + math.sin(a) * drum_r), (math.cos(a) * drum_r, apex + 1.0, zc + math.sin(a) * drum_r)], 0.04, 4)
    dome(0, zc, apex + 1.0, drum_r, 4.4)
    # a pinnacle on each corner of the cross
    for (x, z) in ((x0, zb), (x1, zb), (tx0, tz0), (tx0, tz1), (tx1, tz0), (tx1, tz1)):
        wbox('ashlar', x - 0.3, x + 0.3, -0.1, sp + 0.3, z - 0.3, z + 0.3)
        K.pinnacle(x, sp + 0.3, z, h=1.8, r=0.22)
    # inside: the fountain at the crossing, palms, ferns, wicker chairs, lanterns hung from the ribs
    marble_fountain(0, zc)
    for (x, z, h) in ((-6.5, -15.8, 4.2), (6.5, -15.8, 4.2), (-6.8, -26.0, 5.4), (6.8, -26.0, 5.4), (-11.8, -18.2, 4.6),
                      (-11.8, -22.8, 4.6), (11.8, -18.2, 4.6), (11.8, -22.8, 4.6), (-4.6, -24.8, 3.6), (4.6, -24.8, 3.6)):
        K.palm(x, z, h)
    for (x, z, s) in ((-8.2, -16.6, 1.2), (8.2, -16.6, 1.2), (-8.2, -25.0, 1.3), (8.2, -25.0, 1.3), (-13.0, -20.5, 1.4),
                      (13.0, -20.5, 1.4), (-3.6, -20.5, 0.9), (3.6, -20.5, 0.9), (-6.0, -20.5, 1.1), (6.0, -20.5, 1.1)):
        K.fern(x, z, s)
    for (x, z, rot) in ((-5.0, -16.8, 0.4), (-3.8, -17.6, -0.6), (5.0, -16.8, -0.4), (3.8, -17.6, 0.6)):
        K.chair(x, z, rot, mat='velvet_green')
    K.table(-4.4, -16.6, 0.8, 0.8, h=0.7, mat='trim_cream')
    K.table(4.4, -16.6, 0.8, 0.8, h=0.7, mat='trim_cream')
    for (x, z) in ((0, -15.6), (0, -25.6), (-10.5, zc), (10.5, zc), (-5, -22.8), (5, -18.2)):
        hanging_lantern(x, sp + 2.4, z)
    INTERACT.append(dict(id='view', label='The view over the city', pos=[0, 1.6, zb + 1.4], r=2.5))


def raised_conservatory():
    """The domed conservatory as the photographs show it: carried up over an open carriage arch."""
    import hopkins_house as HH
    TRIM = HH.TRIM
    x0, x1, z0, z1 = HH.CONS
    xc, zc = (x0 + x1) / 2, (z0 + z1) / 2
    hx, hz = (x1 - x0) / 2, (z1 - z0) / 2
    dy = 4.6                                   # the conservatory floor, over the arch
    # the arch: corner piers and pointed openings on the three open sides, a vault, and a balustrade on top
    for (x, z) in ((x1 - 0.4, z0 + 0.4), (x1 - 0.4, z1 - 0.4), (x0 + 0.45, z0 + 0.4), (x0 + 0.45, z1 - 0.4)):
        wbox('ashlar', x - 0.45, x + 0.45, -0.1, dy, z - 0.45, z + 0.45)
        wbox(TRIM, x - 0.52, x + 0.52, -0.1, 0.5, z - 0.52, z + 0.52)
        wbox(TRIM, x - 0.5, x + 0.5, 2.6, 2.8, z - 0.5, z + 0.5)
        K.collide(x - 0.45, 0, z - 0.45, x + 0.45, dy, z + 0.45)
    HH.arch(frame((xc, 0, z1), (1, 0, 0), (0, 0, 1)), hx - 0.9, 2.8, dy, k=0.6)
    HH.arch(frame((xc, 0, z0), (-1, 0, 0), (0, 0, -1)), hx - 0.9, 2.8, dy, k=0.6)
    HH.arch(frame((x1, 0, zc), (0, 0, -1), (1, 0, 0)), hz - 0.85, 2.8, dy, k=0.6)
    wbox('stone', x0, x1, dy - 0.35, dy, z0, z1)                 # the vault's soffit
    wbox('flags', x0, x1, -0.1, 0.02, z0, z1)
    hanging_lantern(xc, dy - 0.35, zc, drop=0.4)
    # the conservatory itself, as before but standing on the arch
    y0 = dy + 0.4
    sp = K.H + dy
    wbox('ashlar', x0, x1 + 0.15, dy, y0, z0 - 0.15, z1 + 0.15)
    wbox(TRIM, x0, x1 + 0.2, y0, y0 + 0.1, z0 - 0.2, z1 + 0.2)
    for (a, b) in (((x0, z1), (x1, z1)), ((x1, z1), (x1, z0)), ((x1, z0), (x0, z0))):
        A, B = Vector((a[0], 0, a[1])), Vector((b[0], 0, b[1]))
        r = (B - A).normalized(); n = Vector((-r.z, 0, r.x)); L = (B - A).length
        F = frame(A, r, n)
        lbox(F, 'glass_cons', 0, L, y0 + 0.1, sp, -0.02, 0.02)
        for k in range(1, 6):
            u = k * L / 6
            lbox(F, 'iron', u - 0.04, u + 0.04, y0 + 0.1, sp, -0.05, 0.05)
            if k in (2, 4): lbox(F, 'glass_lit0', u - L / 6 + 0.08, u - 0.08, y0 + 0.5, sp - 0.6, 0.03, 0.04)
        lbox(F, 'iron', 0, L, y0 + 2.5, y0 + 2.58, -0.05, 0.05)
        lbox(F, TRIM, -0.1, L + 0.1, sp - 0.1, sp + 0.25, 0, 0.3)
    for (x, z) in ((x1, z0), (x1, z1), (x0 + 0.2, z0), (x0 + 0.2, z1)):
        wbox('ashlar', x - 0.25, x + 0.25, y0, sp + 0.25, z - 0.25, z + 0.25)
        K.pinnacle(x, sp + 0.25, z, h=1.6, r=0.2)
    P = HH.pointed(hz, sp, 0.55, 5)
    for (u0, v0), (u1, v1) in zip(P, P[1:]):
        HH.poly('glass_cons', [(x0, v0, zc + u0), (x1, v0, zc + u0), (x1, v1, zc + u1), (x0, v1, zc + u1)])
    HH.poly('glass_cons', [(x1, v, zc + u) for u, v in P])
    tube('iron', [(x1 + 0.05, v, zc + u) for u, v in P], 0.08, 4)
    Q = HH.pointed(hx, sp, 0.55, 5)
    for (u0, v0), (u1, v1) in zip(Q, Q[1:]):
        HH.poly('glass_cons', [(xc + u0, v0, z0), (xc + u0, v0, z1), (xc + u1, v1, z1), (xc + u1, v1, z0)])
    for zz, s in ((z0, -1), (z1, 1)):
        HH.poly('glass_cons', [(xc + u, v, zz) for u, v in Q])
        tube('iron', [(xc + u, v, zz + s * 0.05) for u, v in Q], 0.08, 4)
    top = max(v for _, v in P + Q)
    dome(xc, zc, top - 0.6, 2.4, 3.4)
    K.collide(x0, dy, z0 - 0.3, x1 + 0.3, sp, z1 + 0.3)


def rear_verandas():
    """Two storeys of arcaded veranda across the back, either side of the palm house, as in the rear photographs."""
    zw = -14.225
    d = 2.6                   # (clear of the palm house's transept)
    UF = 6.0
    for (xa, xb) in ((9.8, 15.2), (-15.2, -9.8)):
        zo = zw - d
        xs = list(np.linspace(xa, xb, 3))
        wbox('flags', min(xa, xb) - 0.3, max(xa, xb) + 0.3, -0.1, 0.05, zo - 0.3, zw)
        for x in xs:
            clustered_post(x, zo, 0.05, UF - 0.4, r=0.13, mat='trim_stone', band='trim_stone')
            clustered_post(x, zo, UF, UF + 3.6, r=0.11, mat='trim_stone', band='trim_stone')
        F = frame((xs[0], 0, zo), ((xs[-1] - xs[0]) / abs(xs[-1] - xs[0]), 0, 0), (0, 0, -1))
        L = abs(xs[1] - xs[0])
        for k in range(2):
            G = F @ Matrix.Translation((k * L, 0, 0))
            arch(G, 0.18, L - 0.18, 3.0, UF - 0.4, depth=0.3, mat='trim_stone', spandrel='ashlar')
            arch(G, 0.18, L - 0.18, UF + 1.9, UF + 3.6, depth=0.26, mat='trim_stone', spandrel='ashlar')
            balustrade_panel(G, 0.15, L - 0.15, UF, 1.0, mat='trim_stone')
        # the upper floor and the roof
        wbox('ashlar', min(xa, xb) - 0.2, max(xa, xb) + 0.2, UF - 0.4, UF, zo - 0.25, zw)
        wbox('flags', min(xa, xb) - 0.2, max(xa, xb) + 0.2, UF, UF + 0.05, zo - 0.2, zw)
        bm = bm_for('slate')
        y = UF + 3.6
        for q in ([(min(xa, xb) - 0.4, y, zo - 0.4), (max(xa, xb) + 0.4, y, zo - 0.4), (max(xa, xb) + 0.4, y + 1.3, zw), (min(xa, xb) - 0.4, y + 1.3, zw)],):
            bm.faces.new([bm.verts.new(p) for p in q])
        wbox('trim_stone', min(xa, xb) - 0.45, max(xa, xb) + 0.45, y - 0.25, y, zo - 0.45, zo - 0.2)
        for x in ((xs[0] + xs[1]) / 2, (xs[1] + xs[2]) / 2):
            hanging_lantern(x, UF - 0.4, zo + d / 2, drop=0.7)


def terrace_balustrade(x0, x1, z, y, gaps=(), pier=5.0, lamps=()):
    """A stone balustrade along x at z on ground height y, piers with urns every `pier` metres, open at gaps."""
    xs = np.arange(x0, x1 + 0.01, pier)
    for a, b in zip(xs, xs[1:]):
        if any(a < g1 and b > g0 for g0, g1 in gaps): continue
        wbox('trim_stone', a, b, y, y + 0.18, z - 0.25, z + 0.25)
        wbox('trim_stone', a, b, y + 0.92, y + 1.05, z - 0.22, z + 0.22)
        for xx in np.arange(a + 0.25, b - 0.1, 0.32):
            cyl('trim_stone', (xx, y + 0.18, z), 0.74, 0.07, 0.05, seg=6)
        K.collide(a, y, z - 0.25, b, y + 1.05, z + 0.25)
    for x in xs:
        if any(g0 < x < g1 for g0, g1 in gaps): continue
        wbox('ashlar', x - 0.3, x + 0.3, y, y + 1.25, z - 0.3, z + 0.3)
        wbox('trim_stone', x - 0.36, x + 0.36, y + 1.25, y + 1.37, z - 0.36, z + 0.36)
        if any(abs(x - l) < 0.1 for l in lamps):
            p = gas_lamp_at(x, z, y + 1.37)
        else:
            K.urn(x, y + 1.37, z, 0.8)


def gas_lamp_at(x, z, y, h=3.0):
    from setkit import gas_lamp
    p = gas_lamp(x, z, h=h, y=y)
    K.LAMPS.append(list(p))
    return p


def gazebo(x, z, y):
    """A domed garden pavilion: eight slender columns, a ring beam, a ribbed slate dome and a finial."""
    r = 2.4
    cyl('flags', (x, y - 0.1, z), 0.35, r + 0.6, seg=16)
    for k in range(8):
        a = k * math.pi / 4
        px, pz = x + r * math.cos(a), z + r * math.sin(a)
        cyl('trim_stone', (px, y + 0.25, pz), 3.0, 0.11, 0.09, seg=8)
        K.collide(px - 0.12, y, pz - 0.12, px + 0.12, y + 3.2, pz + 0.12)
        if k % 2:
            nxt = ((k + 1) % 8) * math.pi / 4
            tube('iron', [(px, y + 1.0, pz), (x + r * math.cos(nxt), y + 1.0, z + r * math.sin(nxt))], 0.03, 4)
    cyl('trim_stone', (x, y + 3.25, z), 0.4, r + 0.2, seg=16)
    prof = [((r + 0.1) * math.cos(t * math.pi / 2), y + 3.65 + 2.2 * math.sin(t * math.pi / 2)) for t in np.linspace(0, 1, 7)]
    for (r0, ya), (r1, yb) in zip(prof, prof[1:]):
        cyl('slate', (x, ya, z), yb - ya, max(0.05, r0), max(0.05, r1), seg=16, cap=False)
    for k in range(8):
        a = k * math.pi / 4
        tube('trim_stone', [(x + math.cos(a) * (rr + 0.03), yy, z + math.sin(a) * (rr + 0.03)) for rr, yy in prof], 0.05, 4)
    tube('iron', [(x, y + 5.8, z), (x, y + 7.0, z)], 0.035, 4)
    sphere('gilt_frame', (x, y + 6.2, z), 0.14, 8)
    hanging_lantern(x, y + 3.25, z, drop=0.6)
    wbox('walnut_panel', x - 1.0, x + 1.0, y + 0.65, y + 0.72, z - 0.25, z + 0.25)


def terraces():
    """The gardens behind the house stepping down the hill toward the bay: three terraces held by granite walls
    with balustrades, a grand stair down the middle, hedged parterres, cypresses and two domed pavilions."""
    levels = [(0.0, -17.5, -31.0), (-1.8, -31.0, -40.0), (-3.6, -40.0, -50.0)]   # (y, z near the house, z far)
    for (y, za, zb) in levels:
        wbox('grass', -42, 42, y - 0.25, y - 0.05, zb, za)
    stair = (-4.0, 4.0)
    for (y, za, zb), (y2, _, _) in zip(levels, levels[1:]):
        # the retaining wall at zb, from the lower terrace up to this one, and its balustrade
        wbox('granite', -42, 42, y2 - 0.3, y, zb - 0.35, zb + 0.35)
        wbox('trim_stone', -42.2, 42.2, y - 0.02, y + 0.06, zb - 0.45, zb + 0.45)
        terrace_balustrade(-40, 40, zb, y, gaps=[stair], lamps=(-40.0, -5.0, 5.0, 40.0))
        # the stair: from this terrace down to the next
        steps = int(round((y - y2) / 0.2))
        # solid steps, each a riser lower, running out onto the terrace below
        for k in range(steps):
            ytop = y - (k + 1) * 0.2
            wbox('granite', stair[0], stair[1], y2 - 0.3, ytop, zb - 0.35 - (k + 1) * 0.36, zb - 0.35 - k * 0.36)
        run = steps * 0.36
        for s in stair:  # cheek walls outside the treads, with urns at the foot
            sg = 1 if s > 0 else -1
            c0, c1 = sorted((s, s + sg * 0.55))
            wbox('ashlar', c0, c1, y2 - 0.3, y + 0.9, zb - 0.35 - run, zb + 0.35)
            K.collide(c0, y2, zb - 0.35 - run, c1, y + 0.9, zb + 0.35)
            K.urn((c0 + c1) / 2, y + 0.9, zb - 0.35 - run + 0.3, 0.9)
    # the last wall, down to where the hill falls away
    yl, _, zl = levels[-1]
    wbox('granite', -42, 42, yl - 4.0, yl, zl - 0.35, zl + 0.35)
    terrace_balustrade(-40, 40, zl, yl, lamps=(-40.0, 0.0, 40.0))
    for x in (-42, 42):  # the ends
        wbox('granite', x - 0.35, x + 0.35, levels[-1][0] - 4.0, 0.0, zl, -17.5)
    # parterres: clipped box in squares with flowers, on the lower two terraces, either side of the walk
    for li, (y, za, zb) in enumerate(levels[1:]):
        for sx in (-1, 1):
            for cx in ([sx * 11, sx * 21] if li == 0 else [sx * 11, sx * 21, sx * 31]):   # (the pavilions stand at +-31 above)
                cz = (za + zb) / 2
                for (a0, a1, b0, b1) in ((cx - 4, cx + 4, cz - 3, cz - 2.6), (cx - 4, cx + 4, cz + 2.6, cz + 3),
                                         (cx - 4, cx - 3.6, cz - 3, cz + 3), (cx + 3.6, cx + 4, cz - 3, cz + 3)):
                    wbox('hedge', a0, a1, y - 0.05, y + 0.6, b0, b1)
                    K.collide(a0, y, b0, a1, y + 0.6, b1)
                wbox('flowers', cx - 3.4, cx + 3.4, y - 0.05, y + 0.22, cz - 2.4, cz + 2.4)
        for x in range(-38, 39, 6):
            if abs(x) < 6: continue
            from setkit import cyl as _c
            _c('cypress', (x, y, zb + 0.9), 5.0, 0.6, 0.05, seg=10)
    # the pavilions, at either end of the middle terrace, and a small fountain on the lowest
    gazebo(-31.0, -35.5, -1.8)
    gazebo(31.0, -35.5, -1.8)
    y3 = levels[2][0]
    cyl('marble_white', (0, y3, -45.0), 0.5, 2.4, 2.5, seg=24)
    cyl('water', (0, y3 + 0.42, -45.0), 0.03, 2.2, seg=24)
    cyl('marble_white', (0, y3 + 0.45, -45.0), 1.4, 0.25, 0.18, seg=12)
    sphere('marble_white', (0, y3 + 2.0, -45.0), 0.3, 10)
    K.collide(-2.4, y3, -47.4, 2.4, y3 + 0.5, -42.6)
    INTERACT.append(dict(id='terrace', label='The view from the terrace', pos=[0, y3 + 1.2, -48.6], r=2.5))
