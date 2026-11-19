"""Build four existing story/roster identities and render a cast comparison."""
import math
import sys
import subprocess
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Characters"
CAST = [
    ("ShenYan", "沈砚", "临溪船夫 · 镇水铃 / 逆潮诀"),
    ("LuYing", "陆缨", "旧日押送者 · 旧关印 / 断契剑"),
    ("Chongshan", "重山", "初始修士 · 开山斧"),
    ("Zhuxing", "逐星", "初始修士 · 逐星弓"),
]
if "--preview-only" not in sys.argv:
    for identity, _, _ in CAST:
        builder = str(Path(__file__).with_name("build_cultivator_sample.py"))
        expression = f"import runpy; runpy.run_path({builder!r}, init_globals={{'CHARACTER': {identity!r}}})"
        subprocess.run([bpy.app.binary_path, "--background", "--python-exit-code", "1", "--python-expr", expression], check=True)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
font = bpy.data.fonts.load("C:/Windows/Fonts/msyh.ttc")
text_material = bpy.data.materials.new("Cast captions")
text_material.diffuse_color = (.055, .07, .07, 1)

for i, (identity, name, description) in enumerate(CAST):
    with bpy.data.libraries.load(str(SOURCE / (identity + ".blend"))) as (available, loaded):
        loaded.objects = [identity + "_Sample"]
    model = loaded.objects[0]
    bpy.context.collection.objects.link(model)
    model.location = ((i - 1.5) * 2.0, 0, 0)
    model.rotation_euler = (0, 0, math.radians(-12))
    for body, size, y in [(name, .21, -1.00), (description, .075, -1.30)]:
        bpy.ops.object.text_add(location=(model.location.x, y, .06),
                                rotation=(math.radians(76), 0, 0))
        caption = bpy.context.object
        caption.data.body = body
        caption.data.font = font
        caption.data.align_x = "CENTER"
        caption.data.size = size
        caption.data.materials.append(text_material)

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.04))
floor_material = bpy.data.materials.new("Cast paper")
floor_material.diffuse_color = (.76, .73, .66, 1)
bpy.context.object.data.materials.append(floor_material)

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add(location=(0, -12, 4.6))
camera = bpy.context.object
aim(camera, (0, 0, .95))
camera.data.type = "ORTHO"
camera.data.ortho_scale = 8.9
bpy.context.scene.camera = camera
for location, power, size in [((-3, -4, 7), 850, 5), ((4, -1, 4), 250, 4)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    lamp = bpy.context.object
    lamp.data.energy = power
    lamp.data.size = size
    aim(lamp, (0, 0, 1))

scene = bpy.context.scene
scene.world.color = (.35, .35, .35)
scene.render.engine = "BLENDER_EEVEE"
scene.view_settings.view_transform = "Standard"
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.filepath = str(SOURCE / "StoryCast_Preview.png")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "StoryCast.blend"))
# Publish only a complete frame; Blender on Windows may fail to truncate an existing PNG.
preview = Path(scene.render.filepath)
scene.render.filepath = str(preview.with_suffix(".render.png"))
bpy.ops.render.render(write_still=True)
Path(scene.render.filepath).replace(preview)
