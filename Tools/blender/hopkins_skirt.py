# Ground under the whole neighbourhood.
#
# The prototype modelled the Hopkins lot and California Street, and set the neighbours' houses down around them with
# nothing underneath: seen from the street at night the fog hid it, but on a big screen the houses across the street
# stand on a void and the block is an island over the city. This lays one skirt of ground under everything, built
# from the ground that is there:
#
#   - every upward face of the ground materials (lawns, flags, cobbles, gravel, kerbs, water) is sampled into a grid,
#     keeping the lowest ground in each cell; there the skirt runs a little below it, out of sight
#   - an empty cell takes the height of the nearest ground (a hair below it, so a neighbour's wall that starts at
#     the sidewalk meets it), falling away the further it is from anything built, down Nob Hill toward the city
#
# Run after the warp, in the enlarged house's coordinates (web axes: y up).
import math
from collections import deque
from setkit import bm_for, material, BM

GROUND = ('grass', 'flags', 'cobbles', 'gravel')   # (not kerbs and walls: a wall top would lift the skirt beside it)
CELL = 1.5
MARGIN = 90.0          # how far past the built ground the skirt runs
UNDER = 0.3            # below ground that is there
HAIR = 0.02            # below the nearest ground, where there is none
FLAT = 18.0            # metres of level ground past the last built ground before the hill falls away
FALL = 0.55            # then this much drop per metre
FLOOR = -70.0


def build(mat='skirt'):
    material(mat, '#1b2416', rough=1.0, scale=3.0)
    cells = {}                                     # (i, j) -> lowest ground y
    lo_x = lo_z = math.inf; hi_x = hi_z = -math.inf
    for name, bm in list(BM.items()):
        if not any(name.startswith(g) for g in GROUND): continue
        bm.normal_update()
        for f in bm.faces:
            nrm = f.normal
            if nrm.y < 0.5: continue
            vs = [v.co for v in f.verts]
            xs = [v.x for v in vs]; zs = [v.z for v in vs]; ys = [v.y for v in vs]
            x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
            lo_x, hi_x, lo_z, hi_z = min(lo_x, x0), max(hi_x, x1), min(lo_z, z0), max(hi_z, z1)
            p = vs[0]
            for i in range(math.floor(x0 / CELL), math.floor(x1 / CELL) + 1):
                for j in range(math.floor(z0 / CELL), math.floor(z1 / CELL) + 1):
                    cx = min(max((i + 0.5) * CELL, x0), x1); cz = min(max((j + 0.5) * CELL, z0), z1)
                    y = p.y - (nrm.x * (cx - p.x) + nrm.z * (cz - p.z)) / nrm.y
                    y = min(max(y, min(ys)), max(ys))
                    k = (i, j)
                    if y < cells.get(k, math.inf): cells[k] = y
    if not cells:
        return 0
    i0 = math.floor((lo_x - MARGIN) / CELL); i1 = math.floor((hi_x + MARGIN) / CELL) + 1
    j0 = math.floor((lo_z - MARGIN) / CELL); j1 = math.floor((hi_z + MARGIN) / CELL) + 1
    W, H = i1 - i0, j1 - j0
    h = [[None] * H for _ in range(W)]
    dist = [[math.inf] * H for _ in range(W)]
    q = deque()
    for (i, j), y in cells.items():
        a, b = i - i0, j - j0
        if 0 <= a < W and 0 <= b < H:
            h[a][b] = y - UNDER; dist[a][b] = 0.0
            q.append((a, b, y))
    # spread the nearest ground outward (breadth first; 8 neighbours, so the distance is near enough)
    while q:
        a, b, src = q.popleft()
        d0 = dist[a][b]
        for da, db, step in ((1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1), (1, 1, 1.414), (1, -1, 1.414), (-1, 1, 1.414), (-1, -1, 1.414)):
            na, nb = a + da, b + db
            if not (0 <= na < W and 0 <= nb < H): continue
            nd = d0 + step * CELL
            if nd < dist[na][nb] - 1e-6:
                dist[na][nb] = nd
                h[na][nb] = max(FLOOR, src - HAIR - max(0.0, nd - FLAT) * FALL)
                q.append((na, nb, src))
    # vertices at cell corners, each as low as the cells around it so the skirt never rises through the ground
    bm = bm_for(mat)
    vs = {}
    def vert(a, b):
        k = (a, b)
        if k not in vs:
            ys = [h[x][y] for x in (a - 1, a) for y in (b - 1, b) if 0 <= x < W and 0 <= y < H]
            vs[k] = bm.verts.new(((i0 + a) * CELL, min(ys), (j0 + b) * CELL))
        return vs[k]
    n = 0
    for a in range(W):
        for b in range(H):
            f = bm.faces.new([vert(a, b), vert(a, b + 1), vert(a + 1, b + 1), vert(a + 1, b)])
            f.normal_update()
            if f.normal.y < 0: f.normal_flip()
            n += 1
    return n
