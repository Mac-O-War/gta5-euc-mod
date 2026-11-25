"""Author the EUC rider clip dictionary (euc_rider.ycd.xml) on the GTA ped skeleton.

Run with the project Blender profile:
  blender -b -P tools/blender/anim/build_euc_anims.py -- <rig.blend> <out_dir> [preview_dir]

<rig.blend> is player_zero imported via Sollumz (assets/game_extract/player_zero_rig.blend).

Clips (all are "scrub" clips: the script freezes them and sets the phase every frame):
  lean   phase 0 = full carve left, 0.5 = cruise, 1 = full carve right
  brake  phase 0 = cruise, 1 = hard stop
  air    phase 0 = cruise, 1 = airborne tuck

Poses are described in BODY coordinates (metres, relative to the ped origin at the pelvis):
  x = rider's left, y = forward, z = up. Ground is ~z -1.0. Reference: assets/reference/NOTES.md.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RIG_BLEND, OUT_DIR = argv[0], Path(argv[1]).resolve()
PREVIEW_DIR = Path(argv[2]).resolve() if len(argv) > 2 else None
DICT_NAME = "euc_rider"
FPS = 30
FEET_IN = 0.03  # move each foot this far toward the wheel from the rest stance (pedals sit close together)

# ------------------------------------------------------------------ poses
CRUISE = dict(
    drop=0.07, spine_fwd=8,
    knee_x=0.0,
    hand_L=(0.28, 0.08, -0.10), elbow_L=(0.3, -0.5, 0.0),
    hand_R=(-0.28, 0.08, -0.10), elbow_R=(-0.3, -0.5, 0.0),
)

# Fast carve into a RIGHT turn: deep crouch, knees/hips/shoulders into the turn,
# inside (right) hand reaching low toward the inside knee, outside (left) arm up near the chest.
CARVE_R = dict(
    drop=0.26, hip_x=-0.05, spine_fwd=30, spine_side=-10,
    knee_x=-0.45,
    hand_R=(-0.34, 0.32, -0.42), elbow_R=(-0.6, -0.2, 0.1),
    hand_L=(0.12, 0.36, 0.08), elbow_L=(0.6, -0.2, -0.2),
)

# Hard stop: deep squat, hips back, torso compact and slightly forward, arms thrown up and out.
BRAKE = dict(
    drop=0.34, hip_y=-0.14, spine_fwd=25,
    knee_x=0.0,
    hand_L=(0.50, 0.20, 0.32), elbow_L=(0.6, -0.1, -0.4),
    hand_R=(-0.50, 0.20, 0.32), elbow_R=(-0.6, -0.1, -0.4),
)

# Airborne tuck: very low, torso folded forward, arms forward and compact.
AIR = dict(
    drop=0.40, spine_fwd=45,
    knee_x=0.0,
    hand_L=(0.22, 0.42, -0.28), elbow_L=(0.5, -0.1, -0.2),
    hand_R=(-0.22, 0.42, -0.28), elbow_R=(-0.5, -0.1, -0.2),
)


def mirror(p):
    """Left/right mirror of a pose (x -> -x, swap L/R)."""
    m = dict(p)
    for k in ("hip_x", "knee_x", "spine_side"):
        if k in p:
            m[k] = -p[k]
    for a, b in (("hand_L", "hand_R"), ("elbow_L", "elbow_R")):
        if a in p:
            m[b] = (-p[a][0], p[a][1], p[a][2])
        if b in p:
            m[a] = (-p[b][0], p[b][1], p[b][2])
    return m


CLIPS = {
    "lean": [(0, mirror(CARVE_R)), (20, CRUISE), (40, CARVE_R)],
    "brake": [(0, CRUISE), (20, BRAKE)],
    "air": [(0, CRUISE), (20, AIR)],
}

SPINE = ["SKEL_Spine0", "SKEL_Spine1", "SKEL_Spine2", "SKEL_Spine3"]
KEYED = (["SKEL_ROOT"] + SPINE + ["SKEL_Neck_1", "SKEL_Head"]
         + [f"SKEL_{s}_{b}" for s in "LR" for b in ("Thigh", "Calf", "Foot", "UpperArm", "Forearm", "Hand")])

# ------------------------------------------------------------------ rig helpers
bpy.ops.wm.open_mainfile(filepath=RIG_BLEND)
rig = bpy.data.objects["player_zero"]
pose = rig.pose
for pb in pose.bones:
    pb.rotation_mode = "QUATERNION"

FWD, LEFT, UP = Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))  # GTA ped faces -Y in model space


def body(x=0.0, y=0.0, z=0.0) -> Vector:
    return LEFT * x + FWD * y + UP * z


def upd():
    bpy.context.view_layer.update()


def P(n):
    return pose.bones[n]


def head(n) -> Vector:
    return P(n).matrix.translation.copy()


def rotate(n, R):
    """Rotate bone n by armature-space rotation R about its head (children follow)."""
    pb = P(n)
    M = pb.matrix.copy()
    h = M.translation.copy()
    pb.matrix = Matrix.Translation(h) @ R.to_4x4() @ Matrix.Translation(-h) @ M
    upd()


def aim(n, child, target: Vector):
    """Rotate bone n so that its child's head moves onto target."""
    cur, want = head(child) - head(n), target - head(n)
    rotate(n, cur.rotation_difference(want).to_matrix())


def two_bone(a, b, c, target: Vector, pole: Vector):
    """Analytic two-bone IK: a=root joint, b=middle joint, c=end joint."""
    A, B, C = head(a), head(b), head(c)
    l1, l2 = (B - A).length, (C - B).length
    d = target - A
    dist = max(min(d.length, l1 + l2 - 1e-3), abs(l1 - l2) + 1e-3)
    dn = d.normalized()
    p = pole - A
    p = (p - dn * p.dot(dn)).normalized()
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    knee = A + dn * (l1 * cos_a) + p * (l1 * math.sqrt(max(0.0, 1 - cos_a * cos_a)))
    aim(a, b, knee)
    aim(b, c, A + dn * dist)


def reset():
    for pb in pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    upd()


def apply_pose(p):
    reset()
    rest_ankle = {s: head(f"SKEL_{s}_Foot") for s in "LR"}
    rest_foot = {s: P(f"SKEL_{s}_Foot").matrix.to_3x3() for s in "LR"}
    rest_head = P("SKEL_Head").matrix.to_3x3()

    # hips: drop / shift (feet stay pinned on the pedals, so this bends the knees)
    root = P("SKEL_ROOT")
    M = root.matrix.copy()
    M.translation += body(p.get("hip_x", 0), p.get("hip_y", 0), -p.get("drop", 0))
    root.matrix = M
    upd()

    # spine: forward bend (about the left axis) and side bend (about -forward), spread over 4 bones
    fwd = math.radians(p.get("spine_fwd", 0)) / len(SPINE)
    side = math.radians(p.get("spine_side", 0)) / len(SPINE)
    for n in SPINE:
        rotate(n, Matrix.Rotation(fwd, 3, LEFT) @ Matrix.Rotation(side, 3, -FWD))

    # head: keep looking ahead (undo most of the spine's rotation)
    corr = (rest_head @ P("SKEL_Head").matrix.to_3x3().inverted()).to_quaternion()
    rotate("SKEL_Head", Quaternion().slerp(corr, 0.85).to_matrix())

    # legs: feet pinned at the rest ankle positions, knees pointing forward (+ knee_x sideways)
    for s, sx in (("L", 1), ("R", -1)):
        hip = head(f"SKEL_{s}_Thigh")
        pole = hip + body(p.get("knee_x", 0) + 0.05 * sx, 1.0, -0.3)
        ankle = rest_ankle[s] - LEFT * (sx * FEET_IN)
        two_bone(f"SKEL_{s}_Thigh", f"SKEL_{s}_Calf", f"SKEL_{s}_Foot", ankle, pole)
        cur = P(f"SKEL_{s}_Foot").matrix.to_3x3()
        rotate(f"SKEL_{s}_Foot", rest_foot[s] @ cur.inverted())  # keep the sole flat on the pedal

    # arms
    for s in "LR":
        hx, hy, hz = p[f"hand_{s}"]
        ex, ey, ez = p[f"elbow_{s}"]
        sh = head(f"SKEL_{s}_UpperArm")
        two_bone(f"SKEL_{s}_UpperArm", f"SKEL_{s}_Forearm", f"SKEL_{s}_Hand",
                 body(hx, hy, hz), sh + body(ex, ey, ez))

    # Turn the finished pose 180 degrees about the vertical axis through the ped origin.
    # The imported rig faces -Y, but in-game the clip plays on a ped facing +Y (confirmed in-game:
    # without this the rider rides backwards). A rigid turn keeps every pose correct relative to travel.
    root = P("SKEL_ROOT")
    root.matrix = Matrix.Rotation(math.pi, 4, "Z") @ root.matrix.copy()
    upd()


def key_pose(frame):
    for n in KEYED:
        pb = P(n)
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if n == "SKEL_ROOT":
            pb.keyframe_insert("location", frame=frame)


# ------------------------------------------------------------------ build actions + Sollumz clip dictionary
from bl_ext.user_default.sollumz.ycd.ycdimport import create_anim_obj, create_clip_dictionary_template  # noqa: E402
from bl_ext.user_default.sollumz.sollumz_properties import SollumType  # noqa: E402
from bl_ext.user_default.sollumz.ycd.ycdexport import export_ycd  # noqa: E402

rig.animation_data_create()
cd_obj, clips_obj, anims_obj = create_clip_dictionary_template(DICT_NAME)
actions = {}
for clip, keys in CLIPS.items():
    act = bpy.data.actions.new(f"{clip}_anim")
    rig.animation_data.action = act
    for frame, p in keys:
        apply_pose(p)
        key_pose(frame)
    for fc in act.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    actions[clip] = act

    anim_obj = create_anim_obj(SollumType.ANIMATION)
    anim_obj.parent = anims_obj
    anim_obj.name = f"{clip}_anim"
    ap = anim_obj.animation_properties
    ap.hash = f"{clip}_anim"
    ap.target_id_type = "ARMATURE"
    ap.target_id = rig.data
    ap.action = act

    clip_obj = create_anim_obj(SollumType.CLIP)
    clip_obj.parent = clips_obj
    clip_obj.name = clip
    cp = clip_obj.clip_properties
    cp.hash = clip
    cp.name = f"{clip}.clip"
    last = keys[-1][0]
    cp.duration = last / FPS
    link = cp.animations.add()
    link.animation = anim_obj
    link.start_frame = 0
    link.end_frame = last
    print("CLIP", clip, "frames", last, "keys", [k for k, _ in keys])

rig.animation_data.action = None
OUT_DIR.mkdir(parents=True, exist_ok=True)
xml_path = OUT_DIR / f"{DICT_NAME}.ycd.xml"
ok = export_ycd(cd_obj, str(xml_path))
print("EXPORT", ok, xml_path, xml_path.stat().st_size if xml_path.exists() else 0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT_DIR / f"{DICT_NAME}_authoring.blend"), compress=True)

# ------------------------------------------------------------------ previews
if PREVIEW_DIR:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "SINGLE"
    sc.render.resolution_x, sc.render.resolution_y = 520, 640
    sc.world = bpy.data.worlds.new("w")
    # simple stand-in wheel + pedals so the pose can be judged in context
    bpy.ops.mesh.primitive_cylinder_add(radius=0.26, depth=0.12, location=(0, 0, -1.0 - 0.21 + 0.26), rotation=(0, math.pi / 2, 0))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.985), scale=(0.42, 0.12, 0.02))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.2
    ctr = Vector((0, 0, -0.15))
    # the exported poses are turned 180 degrees (see apply_pose), so mirror the camera positions too
    views = {"side": -body(-4, 0, 0), "front": -body(0, 4, 0), "threeq": -body(-2.6, 2.6, 0.9)}
    for v in views.values():
        v.z = -v.z
    for name, frame_spec in (("cruise", ("lean", 20)), ("carve_right", ("lean", 40)), ("carve_left", ("lean", 0)),
                             ("brake", ("brake", 20)), ("air", ("air", 20))):
        clip, frame = frame_spec
        rig.animation_data.action = actions[clip]
        sc.frame_set(frame)
        for v, off in views.items():
            cam.location = ctr + off
            cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = str(PREVIEW_DIR / f"{name}_{v}.png")
            bpy.ops.render.render(write_still=True)
    print("PREVIEWS", PREVIEW_DIR)
