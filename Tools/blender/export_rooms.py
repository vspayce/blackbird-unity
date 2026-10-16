# Builds the web game's other sets with its own Blender scripts and exports them for Unity: Holmes's suite at the
# Palace Hotel (Chapter II), Brigid's rooms at the St. Mark (Chapter III) and Kearny Street (Chapter V).
#
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
#       --python Tools/blender/export_rooms.py -- <blackbird web repo> <blackbird-unity project> [palace stmark kearny]
#
# Writes Assets/BlackBird/Art/<Set>/<set>.fbx, its Textures/ and <set>.json (colliders, lamps, named places, spawn,
# and for Kearny the cover points and the cable car's run), the same data the web game reads.
import os, sys, json, math, importlib

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
WEB, DST = os.path.expanduser(ARGS[0]), os.path.expanduser(ARGS[1])
ONLY = ARGS[2:] or ['palace', 'stmark', 'kearny']
sys.path.insert(0, os.path.join(WEB, 'art'))
sys.path.insert(0, os.path.join(DST, 'Tools', 'blender'))
import bpy
import setkit
import unity_export as UX


def lamps_as_empties(root, lamps):
    for k, p in enumerate(lamps):
        e = bpy.data.objects.new(f'Lamp_{k}', None)
        bpy.context.scene.collection.objects.link(e)
        e.location = p; e.parent = root


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    setkit.BM.clear()


def out(name):
    d = os.path.join(DST, 'Assets', 'BlackBird', 'Art', name.capitalize())
    os.makedirs(d, exist_ok=True)
    return d


def hotel(name, module, title, spawn, extra_mats=None):
    fresh()
    M = importlib.import_module(module)
    bh = M.bh
    for lst in (bh.COLLIDERS, bh.LAMPS): lst.clear()
    M.PLACES.clear()
    bh.hopkins_materials(); bh.interior_materials(); M.hotel_materials()
    if extra_mats: extra_mats(M)
    M.build_suite(); M.furnish_suite()
    root = setkit.finish(title)
    lamps_as_empties(root, bh.LAMPS)
    d = out(name)
    UX.export_set(os.path.join(d, f'{name}.fbx'), os.path.join(d, 'Textures'), name)
    with open(os.path.join(d, f'{name}.json'), 'w') as f:
        json.dump(dict(colliders=bh.COLLIDERS, lamps=[[round(v, 3) for v in p] for p in bh.LAMPS], places=M.PLACES, spawn=spawn),
                  f, separators=(',', ':'))
    print('ROOM DONE', name, len(bh.LAMPS), 'lamps')


def stmark_mats(M):
    t, n = M.wood_tex('boards', '#4e3622', planks=7)
    M.material('boards', tex=t, nrm=n, rough=0.55, scale=1.4)
    M.material('velvet_blue', '#22305a', rough=0.95)


def kearny():
    fresh()
    M = importlib.import_module('build_city')
    for lst in (M.COLLIDERS, M.LAMPS, M.COVER): lst.clear()
    M.PLACES.clear()
    M.city_materials(); M.street_surface(); M.block_fronts(); M.sutter_view(); M.street_furniture()
    root = setkit.finish('Kearny')
    setkit.BM.clear()
    M.cable_car()
    car = setkit.finish('CableCar')
    car.location = (-M.SUTTER_RUN, -M.SUTTER_Z, 0)
    lamps_as_empties(root, M.LAMPS)
    d = out('kearny')
    UX.export_set(os.path.join(d, 'kearny.fbx'), os.path.join(d, 'Textures'), 'kearny')
    with open(os.path.join(d, 'kearny.json'), 'w') as f:
        json.dump(dict(colliders=M.COLLIDERS, lamps=[[round(v, 3) for v in p] for p in M.LAMPS], cover=M.COVER, places=M.PLACES,
                       spawn=dict(pos=[-8.6, 0, -2.0], yaw=math.pi),
                       cablecar=dict(z=M.SUTTER_Z, x0=-M.SUTTER_RUN, x1=M.SUTTER_RUN, speed=4.5, period=75, length=8.0, width=2.5)),
                  f, separators=(',', ':'))
    print('ROOM DONE kearny', len(M.LAMPS), 'lamps')


if 'palace' in ONLY:
    hotel('palace', 'build_palace', 'Palace', dict(pos=[0, 0, 5.0], yaw=math.pi, watson=[-1.3, 0, -1.4]))
if 'stmark' in ONLY:
    hotel('stmark', 'build_stmark', 'StMark', dict(pos=[0.3, 0, 1.6], yaw=math.pi, watson=[0.9, 0, -0.9]), stmark_mats)
if 'kearny' in ONLY:
    kearny()
print('ROOMS DONE')
