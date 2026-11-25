"""Turn the cleaned-up EUC .blend into GTA V assets (native binary, Gen8 / Legacy).

Run with the project-local Blender profile (Sollumz + PyMateria installed):
  set BLENDER_USER_RESOURCES=C:/code/gta5_euc/tools/blender_user
  blender -b -P tools/blender/build_euc.py -- <in.blend> <out_dir> <model_name> [texture_size]

Produces <model_name>.ydr (drawable with embedded texture) and <model_name>.ytyp (archetype).
"""
import math
import sys
from pathlib import Path

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
blend_in, out_dir, name = str(Path(argv[0]).resolve()), Path(argv[1]).resolve(), argv[2]
tex_size = int(argv[3]) if len(argv) > 3 else 2048
LOWPOLY = "EUC_lowpoly"

out_dir.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend_in)
scene = bpy.context.scene

# --- keep only the game mesh
src = bpy.data.objects[LOWPOLY]
for o in list(bpy.data.objects):
    if o is not src:
        bpy.data.objects.remove(o, do_unlink=True)
if src.name not in scene.collection.objects:
    for c in list(src.users_collection):
        c.objects.unlink(src)
    scene.collection.objects.link(src)

# --- GTA forward is +Y; in-game the source model faces backwards, so turn the mesh 180 degrees.
# Rotate the mesh data directly: glTF imports use quaternion rotation mode, so setting
# rotation_euler on the object silently does nothing.
from mathutils import Matrix
bpy.ops.object.select_all(action="DESELECT")
src.select_set(True)
bpy.context.view_layer.objects.active = src
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
src.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
src.data.update()
src.name = f"{name}_mesh"

# --- mesh attributes Sollumz shaders expect: "UVMap 0" and a "Color 1" vertex colour layer
mesh = src.data
assert mesh.uv_layers, "mesh has no UVs"
mesh.uv_layers[0].name = "UVMap 0"
for extra in list(mesh.uv_layers)[1:]:
    mesh.uv_layers.remove(extra)
if "Color 1" not in mesh.color_attributes:
    col = mesh.color_attributes.new("Color 1", "BYTE_COLOR", "CORNER")
    for d in col.data:
        d.color_srgb = (1.0, 1.0, 1.0, 1.0)

# --- texture: the one real image, downscaled, with a game-friendly name
diffuse = max((i for i in bpy.data.images if i.size[0] > 0), key=lambda i: i.size[0])
for img in list(bpy.data.images):
    if img is not diffuse:
        bpy.data.images.remove(img)
if diffuse.size[0] > tex_size:
    diffuse.scale(tex_size, tex_size)

# Sollumz only embeds DDS data, so compress to DXT1 ourselves and swap the image for the DDS.
import numpy as np
sys.path.insert(0, str(Path(__file__).parent))
from dds_bc1 import write_dds_bc1

w, h = diffuse.size
px = np.empty(w * h * 4, dtype=np.float32)
diffuse.pixels.foreach_get(px)
rgb = px.reshape(h, w, 4)[::-1, :, :3]  # Blender stores bottom row first; DDS wants top row first
dds_path = out_dir / f"{name}_d.dds"
mips = write_dds_bc1(str(dds_path), rgb)
bpy.data.images.remove(diffuse)
diffuse = bpy.data.images.load(str(dds_path))
diffuse.name = f"{name}_d"
diffuse.pack()
print("TEXTURE", diffuse.name, tuple(diffuse.size), "mips", mips, "dds", diffuse.packed_file.data[:4])

# --- Sollumz material + drawable
scene.auto_create_embedded_col = False
scene.create_seperate_drawables = False
bpy.ops.sollumz.autoconvertmaterials()
for mat in src.data.materials:
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE":
            print("  node", n.name, n.image.name if n.image else None)
            if n.name == "DiffuseSampler":
                n.image = diffuse
                n.texture_properties.embedded = True
    print("MATERIAL", mat.name, getattr(mat, "sollum_type", "?"), getattr(mat.shader_properties, "filename", "?"))
for img in list(bpy.data.images):
    if img is not diffuse and img.users == 0:
        bpy.data.images.remove(img)

bpy.ops.sollumz.converttodrawable()
drawable = src.parent
assert drawable is not None, "converttodrawable did not create a drawable parent"
drawable.name = name
print("DRAWABLE", drawable.name, drawable.sollum_type, [c.name for c in drawable.children])

# --- archetype (.ytyp)
bpy.ops.sollumz.createytyp()
ytyp = scene.ytyps[-1]
ytyp.name = name
scene.ytyp_index = len(scene.ytyps) - 1
bpy.ops.object.select_all(action="DESELECT")
drawable.select_set(True)
bpy.context.view_layer.objects.active = drawable
bpy.ops.sollumz.createarchetypefromselected()
arch = ytyp.archetypes[-1]
arch.lod_dist = 150.0
print("ARCHETYPE", arch.name, arch.asset_type, "txd=", arch.texture_dictionary, "lod=", arch.lod_dist)

# --- export native Gen8 (Legacy)
res = bpy.ops.sollumz.export_assets(
    directory=str(out_dir) + "/",
    direct_export=True,
    use_custom_settings=True,
    target_formats={"NATIVE"},
    target_versions={"GEN8"},
    limit_to_selected=False,
    export_ytyps=True,
)
print("EXPORT", res)
for f in sorted(out_dir.rglob("*")):
    if f.is_file():
        print("OUT", f.relative_to(out_dir), f.stat().st_size)
bpy.ops.wm.save_as_mainfile(filepath=str(out_dir / f"{name}_gta.blend"), compress=True)
