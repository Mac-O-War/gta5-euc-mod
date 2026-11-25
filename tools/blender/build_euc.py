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

# --- split off a spinning wheel
# The AI mesh is one piece and has no tire geometry hidden inside the shell, so a plain cut would
# leave a gap that shows when the wheel turns. Instead: remove the tire/rim faces where they are
# visible (outside the shell) from the body, and build a complete ring from four 90-degree copies
# of the clean bottom quarter. The part of the ring passing up inside the shell is hidden by it.
import bmesh

AXLE_Z = 0.26          # axle height (tire radius; the model's origin is the ground contact point)
HALF_W = 0.095         # tire half width; shell side panels sit further out
R_IN, R_OUT = 0.17, 0.30
R_TREAD = 0.25         # tread band; nothing but tire out here below the mudguard
Z_SHELL_BOTTOM = 0.17  # below this height the rim is fully visible
Z_TREAD_TOP = 0.42     # tread band is visible front/back up to about here
SECTOR_DEG = 45.0


def _polar(c):
    y, z = c.y, c.z - AXLE_Z
    return math.hypot(y, z), math.degrees(math.atan2(y, -z))  # angle from straight down


def _visible_wheel_face(c):
    r, _ = _polar(c)
    if abs(c.x) >= HALF_W or r < R_IN or r > R_OUT:
        return False
    return c.z < Z_SHELL_BOTTOM or (r >= R_TREAD and c.z < Z_TREAD_TOP)


wheel = src.copy()
wheel.data = src.data.copy()
scene.collection.objects.link(wheel)
wheel.name = f"{name}_wheel_mesh"

bm = bmesh.new()
bm.from_mesh(src.data)
dead = [f for f in bm.faces if _visible_wheel_face(f.calc_center_median())]
bmesh.ops.delete(bm, geom=dead, context="FACES")
bm.to_mesh(src.data)
bm.free()

bm = bmesh.new()
bm.from_mesh(wheel.data)
keep = {f for f in bm.faces if _visible_wheel_face(f.calc_center_median())
        and abs(_polar(f.calc_center_median())[1]) <= SECTOR_DEG}
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context="FACES")
sector = list(bm.faces)
for k in (1, 2, 3):
    dup = bmesh.ops.duplicate(bm, geom=sector)
    verts = [e for e in dup["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.rotate(bm, verts=verts, cent=(0.0, 0.0, AXLE_Z), matrix=Matrix.Rotation(math.radians(90 * k), 3, "X"))
bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, 0.0, -AXLE_Z))  # wheel origin = axle centre
bm.to_mesh(wheel.data)
bm.free()
print("WHEEL faces", len(wheel.data.polygons), "BODY faces", len(src.data.polygons), "removed", len(dead))

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

# --- Sollumz material + drawables (body and wheel are separate props in-game)
scene.auto_create_embedded_col = False
scene.create_seperate_drawables = True
drawables = {}
for obj, drawable_name in ((src, name), (wheel, f"{name}_wheel")):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.sollumz.autoconvertmaterials()
    for mat in obj.data.materials:
        for n in mat.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.name == "DiffuseSampler":
                n.image = diffuse
                n.texture_properties.embedded = True
        print("MATERIAL", drawable_name, mat.name, getattr(mat.shader_properties, "filename", "?"))
    bpy.ops.sollumz.converttodrawable()
    assert obj.parent is not None, "converttodrawable did not create a drawable parent"
    obj.parent.name = drawable_name
    drawables[drawable_name] = obj.parent
    print("DRAWABLE", drawable_name, [c.name for c in obj.parent.children])
for img in list(bpy.data.images):
    if img is not diffuse and img.users == 0:
        bpy.data.images.remove(img)

# --- archetypes (.ytyp): one file, both props
bpy.ops.sollumz.createytyp()
ytyp = scene.ytyps[-1]
ytyp.name = name
scene.ytyp_index = len(scene.ytyps) - 1
for d in drawables.values():
    bpy.ops.object.select_all(action="DESELECT")
    d.select_set(True)
    bpy.context.view_layer.objects.active = d
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
