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
        spawn=dict(pos=[-2.6, K.STREET_Y, round(K.WALL_Z1 + 1.6, 2)], yaw=math.pi),   # the near sidewalk, at the foot of the front steps
        street=K.STREET_Y, wall=K.WALL_Z1, hall=[-7, 7, -6, 10],
        tower=dict(top=hopkins_house.TOWER_TOP, foot=[9.2, 0, -8.6]),
        colliders=K.COLLIDERS,
        grand=not PLAIN,
    )
    with open(os.path.join(OUT, 'hopkins.json'), 'w') as f:
        json.dump(data, f, separators=(',', ':'))
    print('HOPKINS DONE', len(K.LAMPS), 'lamps')


main()
