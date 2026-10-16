# The Mark Hopkins house for the Unity game: the web prototype's house (art/build_hopkins.py in the blackbird
# repo) built in-process, made grander by hopkins_grand_parts.py, and exported straight into this project.
#
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
#       --python Tools/blender/hopkins_grand.py -- <blackbird web repo> <blackbird-unity project> [--plain]
#
# --plain builds the prototype's house unchanged (for comparison).
#
# Writes Assets/BlackBird/Art/Hopkins/hopkins.fbx, its Textures/ and hopkins.json (lamps, rooms, places to use,
# spawn points; walking uses the real floors and stairs, so there are no ground tables any more).
import os, sys, json, math, importlib

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
WEB = os.path.expanduser(globals().get('WEB') or ARGS[0])
DST = os.path.expanduser(globals().get('DST') or ARGS[1])
PLAIN = '--plain' in ARGS
HERE = os.path.join(DST, 'Tools', 'blender')

sys.path.insert(0, os.path.join(WEB, 'art'))
sys.path.insert(0, HERE)
import bpy
import setkit
import build_hopkins as K
import hopkins_house
import unity_export as UX

OUT = os.path.join(DST, 'Assets', 'BlackBird', 'Art', 'Hopkins')


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    G = None
    if not PLAIN:
        import hopkins_grand_parts as G
        importlib.reload(G)
        G.K = K
        G.patch()                      # swaps in the grand versions of some of the prototype's builders
    HW = None
    if G:
        import hopkins_warp as HW      # the house enlarged: everything built is carried through a warp afterwards
        importlib.reload(HW)
        K.build_hall = lambda R: None  # the new Grand Hall is built after the warp, at its new size
        HW.install([setkit, K, G, hopkins_house], K, G)
    K.hopkins_materials()
    K.interior_materials()
    if G: G.materials()
    R, walls = K.build_house()
    K.build_hall(R)
    K.furnish(R)
    K.dress_rooms(R)
    K.build_solarium_glass()
    hopkins_house.build(K)
    K.build_upper()
    K.build_grounds()
    if G: G.build(R)                   # everything new
    if HW:
        HW.apply(K, G)
        G.grand_hall2(HW)
    if G:
        import hopkins_skirt
        importlib.reload(hopkins_skirt)
        print('skirt cells', hopkins_skirt.build())   # ground under the neighbours, falling away down the hill
    W = HW.W if HW else (lambda p: p)
    def rect(x0, x1, z0, z1):
        a, b = W((x0, 0, z0)), W((x1, 0, z1))
        return [round(min(a[0], b[0]), 3), round(max(a[0], b[0]), 3), round(min(a[2], b[2]), 3), round(max(a[2], b[2]), 3)]
    root = setkit.finish('Hopkins')
    for k, p in enumerate(K.LAMPS):
        e = bpy.data.objects.new(f'Lamp_{k}', None)
        bpy.context.scene.collection.objects.link(e)
        e.location = p; e.parent = root
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.join(DST, 'Art~'), exist_ok=True)   # (Unity ignores folders ending in ~)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(DST, 'Art~', 'hopkins_grand.blend'))
    UX.export_set(os.path.join(OUT, 'hopkins.fbx'), os.path.join(OUT, 'Textures'), 'hopkins')
    data = dict(
        lamps=[[round(v, 3) for v in p] for p in K.LAMPS],
        big_lamps=[[round(v, 3) for v in p] for p in (G.BIG_LAMPS if G else [])],
        rooms=K.ROOMS,
        interact=K.INTERACT + (G.INTERACT if G else []),
        spawn=dict(pos=[round(v, 3) for v in W((0.0, K.STREET_Y, K.WALL_Z1 + 1.6))], yaw=math.pi),   # the near sidewalk, at the foot of the front steps
        street=K.STREET_Y, wall=round(W((0, 0, K.WALL_Z1))[2], 3),
        hall=rect(-7, 7, -6, 10), hall_h=round(W((0, K.HALL_H, 0))[1], 3),
        room_h=round(W((0, K.H, 0))[1], 3),                               # a ground-floor room's ceiling
        house=rect(-15, 15, -14, 14),                                     # indoors, for telling the lamps apart
        palm=[rect(-9, 9, -27.5, -14), rect(-13.5, 13.5, -24, -17)] if G else [],
        tower=dict(top=[round(v, 3) for v in W(hopkins_house.TOWER_TOP)], foot=[round(v, 3) for v in W((9.2, 0, -8.6))]),
        colliders=K.COLLIDERS,
        grand=not PLAIN,
    )
    with open(os.path.join(OUT, 'hopkins.json'), 'w') as f:
        json.dump(data, f, separators=(',', ':'))
    print('HOPKINS DONE', len(K.LAMPS), 'lamps')


main()
