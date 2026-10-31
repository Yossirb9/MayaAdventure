"""Retarget Quaternius' Universal Animation Library (CC0) onto Maya's Mixamo-style skeleton and write a new Maya.glb.

blender -b -P retarget_ual.py -- <Maya.glb> <UAL.glb> <out.glb> [preview_dir]

Method: for every mapped bone the target rest pose is first rotated so its bone direction matches the source rest
pose (handles T-pose vs A-pose), then the source's world-space rotation change from its rest pose is applied.
Hips height changes are scaled by the leg-length ratio. Unmapped bones (hair, fingers) keep their rest pose.
"""
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index('--') + 1:]
maya_path, ual_path, out_path = argv[:3]
preview_dir = argv[3] if len(argv) > 3 else None

# Source (UAL / Unreal names) -> target (Mixamo names, without the "mixamorig:" prefix)
MAP = {
    'pelvis': 'Hips', 'spine_01': 'Spine', 'spine_02': 'Spine1', 'spine_03': 'Spine2', 'neck_01': 'Neck', 'Head': 'Head',
    'clavicle_l': 'LeftShoulder', 'upperarm_l': 'LeftArm', 'lowerarm_l': 'LeftForeArm', 'hand_l': 'LeftHand',
    'clavicle_r': 'RightShoulder', 'upperarm_r': 'RightArm', 'lowerarm_r': 'RightForeArm', 'hand_r': 'RightHand',
    'middle_01_l': 'LeftHandMiddle1', 'middle_02_l': 'LeftHandMiddle2', 'middle_03_l': 'LeftHandMiddle3',
    'middle_01_r': 'RightHandMiddle1', 'middle_02_r': 'RightHandMiddle2', 'middle_03_r': 'RightHandMiddle3',
    'thigh_l': 'LeftUpLeg', 'calf_l': 'LeftLeg', 'foot_l': 'LeftFoot', 'ball_l': 'LeftToeBase',
    'thigh_r': 'RightUpLeg', 'calf_r': 'RightLeg', 'foot_r': 'RightFoot', 'ball_r': 'RightToeBase',
}
# Bone -> the bone whose head defines its direction (for rest-pose alignment).
AIM = {
    'pelvis': 'spine_01', 'spine_01': 'spine_02', 'spine_02': 'spine_03', 'spine_03': 'neck_01', 'neck_01': 'Head',
    'clavicle_l': 'upperarm_l', 'upperarm_l': 'lowerarm_l', 'lowerarm_l': 'hand_l', 'hand_l': 'middle_01_l',
    'clavicle_r': 'upperarm_r', 'upperarm_r': 'lowerarm_r', 'lowerarm_r': 'hand_r', 'hand_r': 'middle_01_r',
    'middle_01_l': 'middle_02_l', 'middle_02_l': 'middle_03_l', 'middle_01_r': 'middle_02_r', 'middle_02_r': 'middle_03_r',
    'thigh_l': 'calf_l', 'calf_l': 'foot_l', 'foot_l': 'ball_l', 'thigh_r': 'calf_r', 'calf_r': 'foot_r', 'foot_r': 'ball_r',
}
CLIPS = {
    'Idle_Loop': 'Idle', 'Walk_Loop': 'Walk', 'Jog_Fwd_Loop': 'Run', 'Sprint_Loop': 'Sprint',
    'Jump_Start': 'JumpStart', 'Jump_Loop': 'Jump', 'Jump_Land': 'JumpLand',
    'Punch_Jab': 'PunchJab', 'Punch_Cross': 'PunchCross', 'Roll': 'Roll',
    'Swim_Fwd_Loop': 'Swim', 'Swim_Idle_Loop': 'SwimIdle', 'Dance_Loop': 'Dance',
    'Hit_Chest': 'Hit', 'Death01': 'Death', 'Crouch_Idle_Loop': 'Crouch',
}

bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb(path):
    objs, acts = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=path)
    new_objs = [o for o in bpy.data.objects if o not in objs]
    arm = next(o for o in new_objs if o.type == 'ARMATURE')
    return arm, new_objs, [a for a in bpy.data.actions if a not in acts]


tgt, tgt_objs, old_actions = import_glb(maya_path)
for a in old_actions:
    a.name = 'Old_' + a.name.split('|')[-1]  # free the short names for the new clips
src, src_objs, ual_actions = import_glb(ual_path)
print('maya actions:', [a.name for a in old_actions])
print('ual actions:', len(ual_actions))


def tname(short):
    for b in tgt.data.bones:
        if b.name == short or b.name.endswith(':' + short):
            return b.name
    return None


pairs = {s: tname(t) for s, t in MAP.items() if tname(t) and s in src.data.bones}
print('mapped', len(pairs), 'of', len(MAP))
sw, tw = src.matrix_world, tgt.matrix_world


def head_w(arm, name):
    return arm.matrix_world @ arm.data.bones[name].head_local


def rest_rot_w(arm, name):
    return (arm.matrix_world @ arm.data.bones[name].matrix_local).to_quaternion()


# Rest-pose alignment per mapped bone.
align = {}
for s, t in pairs.items():
    a = AIM.get(s)
    if a and a in pairs:
        ds = (head_w(src, a) - head_w(src, s)).normalized()
        dt = (head_w(tgt, pairs[a]) - head_w(tgt, t)).normalized()
        align[t] = dt.rotation_difference(ds)
    else:
        align[t] = Quaternion()

src_hips_rest = head_w(src, 'pelvis')
tgt_hips_name = pairs['pelvis']
tgt_hips_rest = head_w(tgt, tgt_hips_name)
ground_s = min(head_w(src, b).z for b in ('foot_l', 'foot_r'))
ground_t = min(head_w(tgt, pairs[b]).z for b in ('foot_l', 'foot_r'))
scale = (tgt_hips_rest.z - ground_t) / max(1e-4, src_hips_rest.z - ground_s)
print('hips scale', round(scale, 3))

order = []  # target bones, parents first
def walk(b):
    order.append(b)
    for c in b.children:
        walk(c)
for b in tgt.data.bones:
    if b.parent is None:
        walk(b)

src_by_tgt = {t: s for s, t in pairs.items()}
tw_inv = tw.inverted()
tw_rot_inv = tw.to_quaternion().inverted()


def assign(arm, action):
    arm.animation_data_create()
    arm.animation_data.action = action
    ad = arm.animation_data
    if hasattr(ad, 'action_slot') and hasattr(action, 'slots') and len(action.slots) > 0 and ad.action_slot is None:
        ad.action_slot = action.slots[0]


scene = bpy.context.scene
for pb in tgt.pose.bones:
    pb.rotation_mode = 'QUATERNION'

made = []
for act in ual_actions:
    base = act.name.split('|')[-1]
    for k in CLIPS:
        if base == k or base.startswith(k):
            base = k
            break
    if base not in CLIPS:
        continue
    out_name = CLIPS[base]
    assign(src, act)
    f0, f1 = (int(round(x)) for x in act.frame_range)
    new = bpy.data.actions.new('RT_' + out_name)
    assign(tgt, new)
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        pose = {}
        for b in order:
            rest = b.matrix_local
            if b.parent:
                rel = b.parent.matrix_local.inverted() @ rest
                default = pose[b.parent.name] @ rel
            else:
                default = rest
            s = src_by_tgt.get(b.name)
            if s:
                qs = (sw @ src.pose.bones[s].matrix).to_quaternion()
                qd = qs @ rest_rot_w(src, s).inverted()
                qt = qd @ align[b.name] @ rest_rot_w(tgt, b.name)
                rot = (tw_rot_inv @ qt).to_matrix().to_4x4()
                pos = default.translation.copy()
                if b.name == tgt_hips_name:
                    delta_w = (sw @ src.pose.bones[s].head) - src_hips_rest
                    pos = tw_inv @ (tgt_hips_rest + delta_w * scale)
                m = Matrix.Translation(pos) @ rot
            else:
                m = default
            pose[b.name] = m
            basis = default.inverted() @ m if not b.parent else (pose[b.parent.name] @ (b.parent.matrix_local.inverted() @ rest)).inverted() @ m
            pb = tgt.pose.bones[b.name]
            pb.rotation_quaternion = basis.to_quaternion()
            pb.keyframe_insert('rotation_quaternion', frame=f)
            if b.name == tgt_hips_name:
                pb.location = basis.translation
                pb.keyframe_insert('location', frame=f)
    new.use_fake_user = True
    made.append(out_name)
    print('retargeted', base, '->', out_name, f1 - f0 + 1, 'frames')

# Only Maya's clips leave this file: drop the library's own actions, and Maya's old clips the library replaces.
for a in ual_actions:
    bpy.data.actions.remove(a)
for a in list(old_actions):
    short = a.name[len('Old_'):]
    if short in made:
        bpy.data.actions.remove(a)
    else:
        a.name = short
        a.use_fake_user = True
for name in made:
    bpy.data.actions['RT_' + name].name = name

if preview_dir:
    # Quick turntable frames of a few clips so the result can be checked without Unity.
    import os
    os.makedirs(preview_dir, exist_ok=True)
    for o in src_objs:
        o.hide_render = True
    cam_data = bpy.data.cameras.new('cam')
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    center = tgt_hips_rest
    cam.location = center + Vector((2.2, -3.4, 0.3))
    cam.rotation_euler = (Vector(center) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.rotation_euler = (0.8, 0.2, 0.6)
    scene.collection.objects.link(sun)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = 480, 480
    for name, frac in (('Run', 0.25), ('PunchJab', 0.35), ('PunchCross', 0.35), ('Swim', 0.3), ('JumpStart', 0.6), ('Dance', 0.3), ('Idle', 0.2)):
        act = bpy.data.actions.get(name)
        if not act:
            continue
        assign(tgt, act)
        f0, f1 = act.frame_range
        scene.frame_set(int(f0 + (f1 - f0) * frac))
        scene.render.filepath = os.path.join(preview_dir, name + '.png')
        bpy.ops.render.render(write_still=True)

# Export Maya only, every action as its own animation.
bpy.ops.object.select_all(action='DESELECT')
for o in tgt_objs:
    o.select_set(True)
assign(tgt, bpy.data.actions[made[0]])
bpy.ops.export_scene.gltf(filepath=out_path, export_format='GLB', use_selection=True,
                          export_animation_mode='ACTIONS', export_force_sampling=True,
                          export_image_format='AUTO', export_yup=True)
print('exported', out_path)
