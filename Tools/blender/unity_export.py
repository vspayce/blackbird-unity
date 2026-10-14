# Shared export pieces for the Unity project: textures written as files beside the model, emission moved
# to base colour (Unity's FBX import reads that reliably), and the FBX settings. Used by export_unity.py
# and hopkins_grand.py.
import os
import bpy


def emission_to_base():
    """Unity's FBX import reads base colour textures reliably but not emission ones: copy each emissive
    material's emission image (or colour) into Base Color. The builder turns it back into emission."""
    for m in bpy.data.materials:
        if not m.node_tree:
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


def write_textures(folder, prefix):
    """Write every image the materials use into folder as <prefix>_<name>.png (copying files that exist,
    saving packed or generated ones), and point the images at them. The FBX then names those files and
    Unity finds them next to the model; embedded textures don't survive Unity's import."""
    import shutil
    os.makedirs(folder, exist_ok=True)
    used = set()
    for m in bpy.data.materials:
        if m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    used.add(n.image)
    for img in used:
        base = ''.join(c if c.isalnum() or c in '-_' else '_' for c in os.path.splitext(img.name)[0])
        src = bpy.path.abspath(img.filepath) if img.filepath else ''
        ext = os.path.splitext(src)[1].lower() if src else ''
        if src and os.path.exists(src) and not img.packed_file and ext in ('.png', '.jpg', '.jpeg'):
            dest = os.path.join(folder, f'{prefix}_{base}{ext}')
            shutil.copyfile(src, dest)
        else:
            dest = os.path.join(folder, f'{prefix}_{base}.png')
            img.filepath_raw = dest
            img.file_format = 'PNG'
            try:
                img.save()
            except Exception as e:  # an image with no pixels
                print('could not save', img.name, e)
                continue
        img.filepath = dest
    return len(used)


def select_only(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.hide_set(False)
        o.hide_select = False
        o.select_set(True)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]


COMMON = dict(apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
              use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP',
              embed_textures=False, use_custom_props=False)




def export_set(fbx_path, tex_dir, prefix):
    """Export every mesh and empty in the open scene as a static set."""
    emission_to_base()
    write_textures(tex_dir, prefix)
    objs = [o for o in bpy.context.scene.objects if o.type in ('MESH', 'EMPTY', 'CURVE')]
    select_only(objs)
    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={'MESH', 'EMPTY', 'OTHER'},
                             bake_anim=False, **COMMON)
    print('exported', fbx_path)
