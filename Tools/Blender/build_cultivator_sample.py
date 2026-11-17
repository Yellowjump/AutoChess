"""Create a flat shaded, palette textured character sample in Blender 4.x."""
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
CHARACTER = globals().get("CHARACTER", "InkSwordsman")
SOURCE = ROOT / "ArtSource/Characters"
OUTPUT = ROOT / "Client/Assets/GameMain/Art/Characters" / CHARACTER
SOURCE.mkdir(parents=True, exist_ok=True)
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

colors = [
    (.74, .49, .29, 1), (.87, .66, .43, 1), (.95, .79, .56, 1), (.30, .16, .10, 1),
    (.035, .047, .052, 1), (.10, .14, .15, 1), (.15, .25, .25, 1), (.22, .36, .35, 1),
    (.76, .76, .63, 1), (.95, .91, .76, 1), (.50, .13, .09, 1), (.72, .23, .12, 1),
    (.12, .17, .19, 1), (.32, .37, .37, 1), (.64, .69, .65, 1), (.36, .27, .15, 1),
]
if CHARACTER == "ShenYan":
    colors[6:10] = [(.16, .25, .27, 1), (.28, .38, .39, 1),
                    (.50, .42, .29, 1), (.76, .69, .51, 1)]
    colors[10] = (.29, .24, .17, 1)
elif CHARACTER == "LuYing":
    colors[6:10] = [(.28, .095, .08, 1), (.43, .15, .11, 1),
                    (.40, .43, .40, 1), (.81, .75, .60, 1)]
elif CHARACTER == "Chongshan":
    colors[6:10] = [(.28, .23, .16, 1), (.42, .34, .22, 1),
                    (.62, .53, .36, 1), (.80, .72, .53, 1)]
    colors[10] = (.19, .22, .21, 1)
elif CHARACTER == "Zhuxing":
    colors[6:10] = [(.19, .23, .34, 1), (.29, .35, .47, 1),
                    (.56, .56, .59, 1), (.86, .83, .70, 1)]
    colors[10] = (.29, .32, .42, 1)
image = bpy.data.images.new("InkSwordsmanPalette", width=64, height=64)
image.pixels = [channel for y in range(64) for x in range(64)
                for channel in colors[(y // 16) * 4 + x // 16]]
image.name = CHARACTER + "Palette"
image.filepath_raw = str(OUTPUT / (CHARACTER + "Palette.png"))
image.file_format = "PNG"
image.save()
material = bpy.data.materials.new(CHARACTER + "_Palette")
material.use_nodes = True
principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
principled.inputs["Roughness"].default_value = .88
texture = material.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = image
texture.interpolation = "Closest"
material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
parts = []


def finish(obj, color):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    uv = obj.data.uv_layers.new(name="Palette")
    coordinate = ((color % 4 + .5) / 4, (color // 4 + .5) / 4)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
        for loop in polygon.loop_indices:
            uv.data[loop].uv = coordinate
    parts.append(obj)
    return obj


def mesh(name, vertices, faces, color):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, color)


def rings(name, levels, sides, color):
    vertices = []
    for z, width, depth, cy in levels:
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.pi / sides
            vertices.append((width * math.cos(a), cy + depth * math.sin(a), z))
    faces = [tuple(reversed(range(sides)))]
    for level in range(len(levels) - 1):
        for i in range(sides):
            a = level * sides + i
            b = level * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range((len(levels) - 1) * sides, len(levels) * sides)))
    return mesh(name, vertices, faces, color)


def block(name, position, scale, color, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Cut corners", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, color)


def segment(name, start, end, radius_start, radius_end, color, sides=6):
    direction = Vector(end) - Vector(start)
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius_start,
                                    radius2=radius_end, depth=direction.length,
                                    location=(Vector(start) + Vector(end)) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return finish(obj, color)


def gem(name, position, scale, color, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, color)


# Feet and split legs keep the silhouette readable from the battle camera.
for side in (-1, 1):
    x = side * .16
    block("Cloth boot", (x, -.075, .105), (.23, .38, .21), 4, .035)
    segment("Trouser", (x, 0, .20), (x * .85, 0, .87), .095, .13, 12)
    block("Boot cuff", (x, 0, .28), (.21, .22, .11), 8, .015)

rings("Short robe", [(.63, .36, .22, 0), (.89, .26, .19, 0),
                    (1.12, .265, .19, 0), (1.43, .32, .18, 0),
                    (1.48, .23, .16, 0)], 8, 6)
block("Wide vermilion sash", (0, -.01, 1.0), (.56, .405, .13), 10, .02)
block("Belt knot", (.13, -.24, 1.0), (.105, .07, .095), 15, .01)
# A slight asymmetric ready stance.
for side in (-1, 1):
    shoulder = (side * .27, 0, 1.38)
    elbow = (side * .43, -.045, 1.12)
    wrist = (side * .49, -.15 if side == -1 else -.075, .93)
    segment("Broad sleeve", elbow, shoulder, .135, .18, 7)
    segment("Forearm", wrist, elbow, .068, .085, 1)
    segment("Wrist binding", wrist, Vector(wrist) + Vector((0, 0, .075)), .075, .075, 8)
    gem("Mitten hand", (wrist[0], wrist[1] - .015, .88), (.08, .07, .115), 1)
    gem("Thumb", (wrist[0] - side * .065, wrist[1] - .035, .90), (.043, .05, .065), 0)

segment("Neck", (0, 0, 1.43), (0, 0, 1.63), .105, .12, 1)
rings("Angular oversized head", [(1.56, .12, .125, -.025), (1.66, .22, .18, 0),
      (1.97, .255, .205, 0), (2.10, .22, .18, .015),
      (2.14, .13, .12, .025)], 8, 1)
for side in (-1, 1):
    gem("Ear", (side * .235, 0, 1.83), (.065, .055, .10), 0)
    # Projecting eyes and massive brows echo the reference's expressive simplicity.
    gem("Eye white", (side * .112, -.186, 1.91), (.062, .033, .047), 9, 2)
    gem("Ink pupil", (side * .107, -.218, 1.91), (.024, .010, .029), 4, 1)
    brow = block("Heavy expressive eyebrow", (side * .109, -.223, 2.002),
                 (.174, .045, .037), 4, .008)
    brow.rotation_euler.y = side * .15
mesh("Long triangular nose", [(-.034, -.20, 1.93), (.034, -.20, 1.93),
     (0, -.29, 1.80), (-.042, -.19, 1.79), (.042, -.19, 1.79)],
     [(0, 1, 2), (0, 2, 3), (1, 4, 2), (2, 4, 3)], 2)
mesh("Quiet crooked smile", [(-.072, -.176, 1.728), (.082, -.177, 1.745),
     (.055, -.185, 1.716), (-.049, -.182, 1.708)], [(0, 1, 2, 3)], 3)
rings("Swept hair cap", [(2.00, .252, .201, .025), (2.13, .235, .19, .025),
      (2.20, .15, .135, .035)], 8, 4)
gem("Topknot", (0, .07, 2.26), (.105, .11, .13), 4)
segment("Hairpin", (-.15, .07, 2.27), (.15, .07, 2.27), .017, .017, 15)
mesh("Asymmetric fringe", [(-.22, -.13, 2.12), (.16, -.18, 2.12),
     (-.07, -.213, 1.99), (-.17, -.19, 2.01)], [(0, 1, 2, 3)], 5)
block("Back hair", (0, .17, 1.89), (.30, .085, .40), 4, .035)
# Slim sword and scabbard across the back, distinct from the robe.
segment("Sword scabbard", (-.26, .245, .72), (.27, .245, 1.56), .042, .045, 12, 4)
segment("Sword grip", (.30, .245, 1.60), (.42, .245, 1.80), .035, .035, 15)
segment("Sword guard", (.18, .245, 1.65), (.38, .245, 1.52), .035, .035, 14, 4)

def remove_parts(prefixes):
    for part in list(parts):
        if any(part.name.startswith(prefix) for prefix in prefixes):
            parts.remove(part)
            bpy.data.objects.remove(part, do_unlink=True)


if CHARACTER == "ShenYan":
    remove_parts(("Sword", "Topknot", "Hairpin"))
    rings("Bamboo rain hat", [(2.07, .43, .36, .015), (2.11, .40, .34, .015),
          (2.29, .09, .08, .015)], 10, 8)
    for side in (-1, 1):
        segment("Hat cord", (side * .23, -.02, 2.08), (side * .10, -.18, 1.64),
                .009, .009, 15, 4)
    segment("Oar handle", (-.62, -.09, .62), (-.62, -.09, 1.72), .025, .025, 15)
    block("Oar blade", (-.62, -.09, .37), (.18, .055, .48), 8, .025)
    block("Boat ledger", (.29, -.23, .90), (.16, .065, .22), 8, .01)
    for z in (.84, .90, .96):
        block("Ledger stitches", (.245, -.268, z), (.02, .012, .018), 4)
    for side in (-1, 1):
        block("Weathered cheek", (side * .17, -.16, 1.79), (.05, .012, .018), 0)
elif CHARACTER == "LuYing":
    remove_parts(("Topknot", "Hairpin", "Back hair"))
    gem("High tied hair", (0, .08, 2.23), (.115, .11, .12), 4)
    segment("Ponytail upper", (0, .10, 2.23), (0, .31, 2.04), .085, .07, 4)
    segment("Ponytail lower", (0, .31, 2.04), (0, .29, 1.72), .07, .035, 4)
    block("Hair tie", (0, .12, 2.25), (.20, .06, .045), 11, .01)
    for side in (-1, 1):
        block("Iron shoulder", (side * .31, -.015, 1.40), (.27, .30, .15), 13, .035)
        block("Forearm plate", (side * .46, -.115, 1.04), (.12, .12, .18), 12, .015)
    mesh("Cross body armor", [(-.22, -.204, 1.35), (.21, -.204, 1.35),
         (.20, -.235, 1.08), (-.20, -.235, 1.08)], [(0, 1, 2, 3)], 12)
    block("Stolen pass seal", (.27, -.24, .83), (.105, .075, .16), 15, .015)
    block("Seal mark", (.27, -.282, .83), (.055, .012, .075), 11)
    segment("Brow scar", (-.18, -.20, 1.98), (-.135, -.213, 1.93), .008, .008, 0, 4)
elif CHARACTER == "Chongshan":
    remove_parts(("Sword", "Topknot", "Hairpin", "Swept hair", "Asymmetric fringe", "Back hair"))
    rings("Cropped hair", [(2.04, .25, .20, .02), (2.14, .22, .18, .02),
          (2.16, .17, .14, .025)], 8, 4)
    gem("Short angular beard", (0, -.11, 1.63), (.145, .105, .09), 4)
    block("Heavy shoulder guard", (-.33, 0, 1.43), (.32, .36, .22), 12, .04)
    segment("Axe haft", (.65, -.08, .15), (.65, -.08, 1.78), .035, .035, 15)
    mesh("Mountain axe blade", [(.62, -.12, 1.73), (.98, -.12, 1.86),
         (1.08, -.12, 1.47), (.64, -.12, 1.47), (.62, -.04, 1.73),
         (.98, -.04, 1.86), (1.08, -.04, 1.47), (.64, -.04, 1.47)],
         [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2),
          (2, 6, 7, 3), (3, 7, 4, 0)], 13)
    segment("Axe cutting edge", (1.01, -.13, 1.84), (1.11, -.13, 1.49), .026, .026, 14, 4)
elif CHARACTER == "Zhuxing":
    remove_parts(("Sword", "Topknot", "Hairpin", "Back hair"))
    rings("Short hood mantle", [(1.31, .37, .23, .06), (1.50, .28, .19, .04),
          (1.55, .13, .13, .025)], 8, 7)
    segment("Tied hair", (0, .17, 2.10), (0, .27, 1.88), .075, .025, 4)
    block("Quiver", (.16, .28, 1.27), (.14, .13, .48), 15, .025)
    for x in (.115, .16, .205):
        segment("Arrow shaft", (x, .28, 1.39), (x, .28, 1.85), .008, .008, 8, 4)
        block("Arrow feather", (x, .28, 1.80), (.028, .025, .10), 9)
    bow_points = [(-.55, -.12, .39), (-.71, -.12, .66), (-.76, -.12, 1.02),
                  (-.71, -.12, 1.37), (-.55, -.12, 1.62)]
    for start, end in zip(bow_points, bow_points[1:]):
        segment("Curved bow", start, end, .025, .025, 15)
    segment("Bowstring", bow_points[0], bow_points[-1], .006, .006, 8, 4)
    gem("Star clasp", (.12, -.24, 1.44), (.045, .02, .045), 9)

# Keep named parts for rigid low-poly skin binding in Unity.
bpy.ops.object.select_all(action="DESELECT")
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (CHARACTER + "_Parts.fbx")), use_selection=True,
                         object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_anim=False, add_leaf_bones=False, path_mode="AUTO")
bpy.ops.object.select_all(action="DESELECT")
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
hero = bpy.context.object
hero.name = CHARACTER + "_Sample"
bpy.context.scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
if CHARACTER == "Chongshan":
    hero.scale = (1.22, 1.10, .97)
elif CHARACTER == "Zhuxing":
    hero.scale = (.94, .94, 1.03)
elif CHARACTER == "LuYing":
    hero.scale = (.95, .95, 1.02)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
triangles = sum(len(face.vertices) - 2 for face in hero.data.polygons)
hero["triangle_count"] = triangles
hero["authoring_note"] = "Static style sample. Feet at origin, face -Y, palette UV, flat shading."
bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (CHARACTER + ".fbx")), use_selection=True,
                         object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_anim=False, add_leaf_bones=False, path_mode="AUTO")

# Presentation scene is stored separately from the exported mesh.
hero.location.x = -2.25
hero.rotation_euler.z = math.radians(-18)
side = hero.copy()
side.data = hero.data
bpy.context.collection.objects.link(side)
side.location.x = 0
side.rotation_euler.z = math.radians(90)
battle = hero.copy()
battle.data = hero.data
bpy.context.collection.objects.link(battle)
battle.location = (2.25, 0, .60)
battle.rotation_euler = (math.radians(36), 0, math.radians(-20))

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.025))
floor = bpy.context.object
floor.name = "Preview floor"
floor_material = bpy.data.materials.new("Warm paper stage")
floor_material.diffuse_color = (.72, .69, .61, 1)
floor.data.materials.append(floor_material)

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add(location=(0, -10, 4.6))
camera = bpy.context.object
aim(camera, (0, 0, 1.1))
camera.data.type = "ORTHO"
camera.data.ortho_scale = 8.0
bpy.context.scene.camera = camera
for location, power, size in [((-3, -4, 7), 650, 5), ((4, -1, 4), 220, 4)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    lamp = bpy.context.object
    lamp.data.energy = power
    lamp.data.shape = "DISK"
    lamp.data.size = size
    aim(lamp, (0, 0, 1))
scene = bpy.context.scene
scene.world.color = (.35, .35, .35)
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "Standard"
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(SOURCE / (CHARACTER + "_Preview.png"))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (CHARACTER + ".blend")))
bpy.ops.render.render(write_still=True)
print(f"CHARACTER_TRIANGLES={triangles}")
