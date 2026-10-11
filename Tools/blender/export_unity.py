# Exports Chapter I's set and cast from the web prototype's .blend files to FBX for Unity.
#
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
#       --python Tools/blender/export_unity.py -- <blackbird web repo> <blackbird-unity project> [names...]
#
# or, from a Blender session (e.g. over MCP): set SRC and DST, then exec() this file.
#
# Why FBX and not the game's GLBs: those carry WebP textures and Draco geometry, which Unity's glTF
# importer can't read. FBX from the .blend files keeps full textures and the rig, and Unity imports
# it natively as a Humanoid (see Assets/BlackBird/Editor/Setup/ProjectSetup.cs).
import os, sys
import bpy

CAST = ['holmes', 'watson', 'archer', 'polhaus', 'kelly']


def args():
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    src = globals().get('SRC') or (a[0] if a else None)
    dst = globals().get('DST') or (a[1] if len(a) > 1 else None)
    only = globals().get('ONLY') or a[2:]
    if not src or not dst:
        raise SystemExit('usage: ... -- <web repo> <unity project> [names...]')
    return os.path.expanduser(src), os.path.expanduser(dst), only


def emission_to_base():
    """Unity's FBX import reads base colour textures reliably but not emission ones: copy each emissive
    material's emission image (or colour) into Base Color. The builder turns it back into emission."""
    for m in bpy.data.materials:
        if not m.use_nodes or not m.node_tree:
            continue
        p = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if not p:
            continue
        strength = p.inputs.get('Emission Strength')
        emit = p.inputs.get('Emission Color') or p.inputs.get('Emission')
        if not strength or not emit or strength.default_value <= 0:
            continue
        base = p.inputs['Base Color']
        for l in list(base.links):
            m.node_tree.links.remove(l)
        if emit.is_linked:
            m.node_tree.links.new(emit.links[0].from_socket, base)
        else:
            base.default_value = emit.default_value


def select_only(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.hide_set(False)
        o.hide_select = False
        o.select_set(True)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]


COMMON = dict(apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
              use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='COPY',
              embed_textures=True, use_custom_props=False)


def export_set(src, dst):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(src, 'art', 'set', 'burritt.blend'))
    emission_to_base()
    objs = [o for o in bpy.context.scene.objects if o.type in ('MESH', 'EMPTY', 'CURVE')]
    select_only(objs)
    out = os.path.join(dst, 'Assets', 'BlackBird', 'Art', 'Set', 'burritt.fbx')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY', 'OTHER'},
                             bake_anim=False, **COMMON)
    print('exported', out)


def export_character(src, dst, name):
    path = os.path.join(src, 'art', 'humans', f'{name}.blend')
    if not os.path.exists(path):
        print('missing', path)
        return
    bpy.ops.wm.open_mainfile(filepath=path)
    rig = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
    if not rig:
        print('no armature in', path)
        return
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and (o.parent == rig or any(
        md.type == 'ARMATURE' and md.object == rig for md in o.modifiers))]
    # export the bind pose as the rest pose, with every action as its own take
    if rig.animation_data:
        rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    select_only([rig] + meshes)
    out = os.path.join(dst, 'Assets', 'BlackBird', 'Art', 'Characters', f'{name}.fbx')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, primary_bone_axis='Y', secondary_bone_axis='X',
                             armature_nodetype='NULL', **COMMON)
    print('exported', out, [a.name for a in bpy.data.actions])


def main():
    src, dst, only = args()
    if not only or 'burritt' in only:
        export_set(src, dst)
    for n in CAST:
        if not only or n in only:
            export_character(src, dst, n)


main()
