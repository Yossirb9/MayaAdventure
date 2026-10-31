"""Blender (headless) helper for Maya's expressions.

blender -b --python face_projection.py -- <Maya.gltf> <out_dir>

Writes, for the face seen straight from the front (orthographic camera, FACE_* below):
  front.png      - the face's albedo (unlit texture colours), 1024x1024
  texels.npz     - every texel of the 2048 texture that is visible in that view:
                   tx, ty (texel), fx, fy (front-image pixel), facing (normal . towards camera)
Painting on front.png and pushing the paint back through texels.npz (make_faces.py) keeps expressions
straight and symmetric even though the face is split over several UV islands.
"""
import bpy, sys, os
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
os.makedirs(out, exist_ok=True)
FACE_X, FACE_Z, FACE_SIZE, IMG = 0.0, 1.37, 0.34, 1024
TEX = 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
if arm:
    arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
mesh_obj = max((o for o in bpy.context.scene.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
scene = bpy.context.scene

# ---------- 1. front albedo render ----------
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'FLAT'
scene.display.shading.color_type = 'TEXTURE'
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = scene.render.resolution_y = IMG
scene.render.film_transparent = True
cam = bpy.data.cameras.new("cam"); cam.type = 'ORTHO'; cam.ortho_scale = FACE_SIZE
co = bpy.data.objects.new("cam", cam); scene.collection.objects.link(co); scene.camera = co
co.location = Vector((FACE_X, -5.0, FACE_Z))
co.rotation_euler = (1.5707963, 0, 0)
scene.render.filepath = os.path.join(out, "front.png")
bpy.ops.render.render(write_still=True)

# ---------- 2. bake world position + normal into texture space ----------
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
scene.cycles.device = 'CPU'
dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.context.scene.objects:
    o.select_set(False)
mesh_obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_obj

def bake(kind):
    img = bpy.data.images.new(kind, TEX, TEX, float_buffer=True, alpha=True)
    for mat in mesh_obj.data.materials:
        nt = mat.node_tree
        nodes = nt.nodes
        geo = nodes.new('ShaderNodeNewGeometry')
        emit = nodes.new('ShaderNodeEmission')
        out_node = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
        tex = nodes.new('ShaderNodeTexImage'); tex.image = img
        nt.links.new(geo.outputs['Position' if kind == 'pos' else 'Normal'], emit.inputs['Color'])
        nt.links.new(emit.outputs['Emission'], out_node.inputs['Surface'])
        nodes.active = tex
    bpy.ops.object.bake(type='EMIT', margin=0)
    a = np.array(img.pixels[:], dtype=np.float32).reshape(TEX, TEX, 4)
    return a[::-1]  # rows from the top, like the PNG

pos = bake('pos')
nrm = bake('nrm')
covered = pos[:, :, 3] > 0.5

# ---------- 3. which texels does the front camera see? ----------
e = mesh_obj.evaluated_get(dg)
me = e.to_mesh()
me.transform(mesh_obj.matrix_world)
bvh = BVHTree.FromPolygons([v.co for v in me.vertices], [p.vertices for p in me.polygons])
half = FACE_SIZE / 2
P = pos[:, :, :3]
N = nrm[:, :, :3]
facing = -N[:, :, 1]  # camera looks along +Y, the face looks towards -Y
inside = covered & (np.abs(P[:, :, 0] - FACE_X) < half) & (np.abs(P[:, :, 2] - FACE_Z) < half) & (facing > 0.05)
ty, tx = np.nonzero(inside)
keep = []
for y, x in zip(ty, tx):
    p = P[y, x]
    hit = bvh.ray_cast(Vector((p[0], -5.0, p[2])), Vector((0, 1, 0)))
    keep.append(hit[0] is not None and abs(hit[0].y - p[1]) < 0.004)
keep = np.array(keep, dtype=bool)
tx, ty = tx[keep], ty[keep]
fx = (P[ty, tx, 0] - (FACE_X - half)) / FACE_SIZE * IMG
fy = ((FACE_Z + half) - P[ty, tx, 2]) / FACE_SIZE * IMG
np.savez_compressed(os.path.join(out, "texels.npz"), tx=tx, ty=ty, fx=fx.astype(np.float32), fy=fy.astype(np.float32),
                    facing=facing[ty, tx].astype(np.float32))
print("TEXELS", len(tx))
