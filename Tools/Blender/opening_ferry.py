"""Build and animate the Qinghe ferry for the opening cinematic in Blender."""

from dataclasses import dataclass
from math import cos, pi, sin
from random import Random

import bpy
from mathutils import Vector


OARLOCK = (1.54, .75, .73)
HAND_GRIPS = (-.84, -.51)


@dataclass
class Ferry:
    root: bpy.types.Object
    torso: bpy.types.Object
    head: bpy.types.Object
    shoulders: tuple
    arms: tuple
    oar: bpy.types.Object
    lantern: bpy.types.Object


def material(name, color, weather=0, emission=0):
    mat = bpy.data.materials.new("Ferry | " + name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    surface = nodes.get("Principled BSDF")
    surface.inputs["Base Color"].default_value = (*color, 1)
    surface.inputs["Roughness"].default_value = .93
    if emission:
        surface.inputs["Emission Color"].default_value = (*color, 1)
        surface.inputs["Emission Strength"].default_value = emission
    if weather:
        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = weather
        noise.inputs["Detail"].default_value = 3
        ramp = nodes.new("ShaderNodeValToRGB")
        for stop, factor in zip(ramp.color_ramp.elements, (.49, 1.37)):
            stop.color = (*(c * factor for c in color), 1)
        links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], surface.inputs["Base Color"])
    return mat


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    return obj


def mesh(name, points, faces, mat, parent):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(mat)
    return obj


def block(name, location, scale, mat, parent):
    bpy.ops.mesh.primitive_cube_add(size=2)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = location
    obj.scale = scale
    obj.data.materials.append(mat)
    return obj


def oval(name, location, scale, mat, parent, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = location
    obj.scale = scale
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def pole(name, mat, parent, tip=1.0, sides=8):
    points = [(radius * cos(i * 2 * pi / sides), radius * sin(i * 2 * pi / sides), z)
              for z, radius in ((-.5, 1), (.5, tip)) for i in range(sides)]
    faces = [tuple(reversed(range(sides))), tuple(range(sides, 2 * sides))]
    faces.extend((i, (i + 1) % sides, (i + 1) % sides + sides, i + sides)
                 for i in range(sides))
    return mesh(name, points, faces, mat, parent)


def fit(obj, start, end, width, depth=None):
    start = Vector(start)
    end = Vector(end)
    length = end - start
    obj.location = (start + end) / 2
    obj.rotation_euler = length.to_track_quat("Z", "Y").to_euler()
    obj.scale = (width, width if depth is None else depth, length.length)
    return obj


def spar(name, start, end, width, mat, parent, tip=1.0, sides=8):
    return fit(pole(name, mat, parent, tip, sides), start, end, width)


def build_hull(root, wood, dark, edge):
    sections = [(-2.45, .06, .60), (-2.16, .32, .49), (-1.53, .65, .43),
                (-.75, .77, .40), (.35, .77, .40), (1.35, .68, .42),
                (2.00, .43, .49), (2.25, .13, .58)]
    sides = [(-1, 1), (-.92, .55), (-.68, .17), (-.32, -.05),
             (.32, -.05), (.68, .17), (.92, .55), (1, 1)]
    points = [(x, w * y, top * z) for x, w, top in sections for y, z in sides]
    n = len(sides)
    faces = [tuple(reversed(range(n))), tuple(range(len(points) - n, len(points)))]
    faces.extend((i*n+j, i*n+j+1, (i+1)*n+j+1, (i+1)*n+j)
                 for i in range(len(sections) - 1) for j in range(n - 1))
    hull = mesh("Joined ferry hull", points, faces, wood, root)
    hull.data.materials.append(dark)
    for polygon in hull.data.polygons:
        if polygon.index > 1 and polygon.index % (n-1) in (4, 5):
            polygon.material_index = 1
    for side in (-1, 1):
        for number, ((x0, w0, z0), (x1, w1, z1)) in enumerate(zip(sections, sections[1:])):
            spar("Gunwale", (x0, side*w0, z0), (x1, side*w1, z1), .038, edge, root)
            spar("Hull plank seam", (x0, side*w0*.94, z0*.56),
                (x1, side*w1*.94, z1*.56), .015, dark, root)
            if number in (2, 3, 4):
                spar("Thin ochre trim", (x0, side*w0, z0-.036), (x1, side*w1, z1-.036),
                    .015, wood, root)
    for i in range(18):
        x = -2.02 + i * .226
        width = .64 - .28 * (abs(x) / 2.18)**3
        block("Ferry deck plank", (x, 0, .377), (.108, width, .025),
              wood if i % 4 else edge, root)
    for x in (-1.6, -.87, -.02, .68):
        block("Passenger bench", (x, 0, .55), (.115, .58, .045), edge, root)


def roof_points(x, span=.78):
    return [(x, span * sin(angle), 1.27 + .40 * cos(angle))
            for angle in [-1.52 + i * 3.04 / 12 for i in range(13)]]


def build_canopy(root, reed, reed_edge, wood):
    rng = Random(1209)
    for i in range(18):
        x = -1.87 + i * .147
        start = roof_points(x)
        end = roof_points(x + .154)
        roof = mesh("Woven reed canopy", start + end,
                    [(j, j + 13, j + 14, j + 1) for j in range(12)],
                    reed if i % 4 else reed_edge, root)
        roof.data.materials.append(reed_edge)
        for face in roof.data.polygons:
            if rng.random() < .13:
                face.material_index = 1
    for x in (-1.87, -1.70, -.84, .09, .64):
        arch = roof_points(x)
        for first, second in zip(arch, arch[1:]):
            spar("Canopy bamboo rib", first, second, .019, reed_edge, root, sides=6)
    for side in (-1, 1):
        spar("Canopy beam", (-1.88, side*.76, 1.28), (.82, side*.76, 1.28), .030, wood, root)
        for x in (-1.65, .60):
            spar("Canopy post", (x, side*.57, .42), (x, side*.71, 1.32), .034, wood, root)
        for i in range(21):
            x = -1.82 + .126*i
            spar("Frayed reed edge", (x, side*.76, 1.30),
                (x+rng.uniform(-.028, .028), side*rng.uniform(.75, .79), rng.uniform(1.20, 1.25)),
                .010, reed_edge, root, sides=5)


def build_lantern(root, lamp, wood):
    # Its origin is the attachment, so the swing stays tied to the beam.
    lantern = empty("Stern lantern swing", root, (.61, .73, 1.28))
    spar("Hemp lantern tie", (0, 0, 0), (0, 0, -.08), .010, wood, lantern)
    oval("Warm oil lantern", (0, 0, -.18), (.082, .078, .115), lamp, lantern)
    for z in (-.08, -.28):
        fit(pole("Lantern cap", wood, lantern, sides=10), (0, 0, z-.012), (0, 0, z+.012), .084)
    for i in range(8):
        angle = i*pi/4
        spar("Lantern frame", (.056*cos(angle), .054*sin(angle), -.09),
            (.068*cos(angle), .064*sin(angle), -.27), .006, wood, lantern, sides=5)
    return lantern


def build_passengers(root, clothes, hat):
    for x, y, size in [(-1.45, -.17, .85), (-.92, .10, 1.0), (-.34, -.16, .88), (.16, .12, .92)]:
        oval("Passenger under canopy", (x, y, .73), (.19, .15, .26*size), clothes, root)
        oval("Passenger head", (x-.06, y, .73+.25*size), (.11, .10, .13*size), hat, root)


def build_ferryman(root, robe, sleeves, trousers, wood, skin, hat, straw):
    legs = ((1.04, .23, .425), (1.60, .22, .425))
    hips = ((1.23, .27, .98), (1.48, .29, .98))
    for foot, hip in zip(legs, hips):
        ankle = Vector(foot) + Vector((0, 0, .08))
        knee = (Vector(foot) + Vector(hip))*.5 + Vector((-.07, .10, .06))
        oval("Cloth shoe", foot, (.132, .075, .05), wood, root)
        spar("Trouser cuff", ankle, knee, .088, trousers, root, tip=1.2)
        spar("Trouser leg", knee, hip, .108, trousers, root, tip=1.13)

    torso = empty("Ferryman spine", root, (1.36, .27, .92))
    fit(pole("Wrapped ferryman coat", robe, torso, tip=1.32), (0, 0, -.07), (0, 0, .47), .181, .14)
    oval("Shoulders beneath coat", (0, 0, .40), (.265, .147, .134), robe, torso)
    oval("Woven sash", (0, .008, .078), (.190, .157, .055), trousers, torso)
    spar("Fold of crossing collar", (-.12, .108, .47), (.061, .16, .15), .026, sleeves, torso)
    spar("Sash tail", (.17, .095, .071), (.22, .102, -.18), .034, sleeves, torso, tip=.7)

    head = empty("Ferryman neck", torso, (0, .006, .48))
    spar("Ferryman neck wrap", (0, 0, 0), (0, 0, .09), .082, sleeves, head)
    oval("Ferryman head", (0, .004, .167), (.117, .103, .145), skin, head)
    oval("Ferryman brow", (0, .095, .168), (.048, .039, .031), skin, head)
    oval("Hair beneath the hat", (0, -.052, .19), (.119, .076, .126), trousers, head)
    fit(pole("Bamboo hat", hat, head, tip=.16, sides=22), (0, 0, .255), (0, 0, .46), .286)
    fit(pole("Bamboo hat rim", straw, head, tip=.97, sides=22), (0, 0, .248), (0, 0, .265), .29)
    for i in range(22):
        a = i*2*pi/22
        spar("Hat weave ridge", (.28*cos(a), .28*sin(a), .268),
            (.046*cos(a), .046*sin(a), .455), .006, straw, head, sides=5)

    shoulders = (Vector((-.223, .04, .40)), Vector((.223, .04, .40)))
    arms = tuple((pole("Ferryman upper sleeve", robe, root, tip=.91),
                  pole("Ferryman forearm", sleeves, root, tip=.72),
                  oval("Ferryman hand", (0, 0, 0), (.055, .053, .064), skin, root))
                 for _ in shoulders)
    return torso, head, shoulders, arms


def build_ferry():
    """Return the ferry and articulated ferryman in boat-local coordinates."""
    root = empty("Qinghe ferry")
    wood = material("weathered wood", (.114, .084, .061), 7)
    dark = material("wet hull", (.039, .031, .027), 6)
    edge = material("worn gunwale", (.177, .131, .087), 14)
    reed = material("canopy straw", (.224, .169, .100), 20)
    reed_edge = material("dark woven bindings", (.153, .110, .066), 15)
    robe = material("slate coat", (.096, .118, .125), 14)
    sleeves = material("slate cloth hem", (.072, .083, .084), 14)
    trousers = material("charcoal cloth", (.038, .044, .044), 13)
    skin = material("weathered skin", (.235, .145, .094), 12)
    lamp = material("oil lantern paper", (.84, .38, .101), emission=2.1)

    build_hull(root, wood, dark, edge)
    build_canopy(root, reed, reed_edge, dark)
    build_passengers(root, trousers, wood)
    lantern = build_lantern(root, lamp, dark)
    torso, head, shoulders, arms = build_ferryman(root, robe, sleeves, trousers, dark, skin, reed, reed_edge)

    # The offset brace ties the oarlock to the stern deck, with room to sweep outside the gunwale.
    x, y, z = OARLOCK
    spar("Oarlock brace", (x, .37, .40), (x, y, z - .08), .051, dark, root)
    for side in (-1, 1):
        spar("Oarlock cheek", (x + side*.057, y, z - .12),
            (x + side*.070, y, z + .075), .023, edge, root, sides=6)

    oar = empty("Ferryman's oar", root, OARLOCK)
    spar("Wooden oar shaft", (0, 0, -1.00), (0, 0, .80), .029, edge, oar, tip=.79)
    blade = mesh("Shaped oar blade", [(-.029, 0, .67), (.029, 0, .67), (.096, 0, .83),
                (.104, 0, 1.00), (.059, 0, 1.075), (-.068, 0, 1.075),
                (-.108, 0, 1.00), (-.095, 0, .83)], [tuple(range(8))], wood, oar)
    blade.modifiers.new("Blade thickness", "SOLIDIFY").thickness = .031
    for _, _, hand in arms:
        hand.parent = oar
    for spacing, (_, _, hand) in zip(HAND_GRIPS, arms):
        hand.location = (0, 0, spacing)
    return Ferry(root, torso, head, shoulders, arms, oar, lantern)


def elbow_for(shoulder, hand, sign):
    reach = hand - shoulder
    across = Vector((sign, -.18, -.76))
    normal = reach.normalized()
    bend = (across - across.dot(normal)*normal).normalized()
    joint = (shoulder + hand) / 2
    return joint + bend * max(.06, (.35**2 - (reach.length / 2)**2)**.5
                              if reach.length < .7 else .06)


def key(obj, frame, *channels):
    for channel in channels:
        obj.keyframe_insert(data_path=channel, frame=frame)


def aim(obj, direction, frame, previous):
    rotation = direction.to_track_quat("Z", "Y")
    # A quaternion and its negation represent the same pose, but must stay on one hemisphere
    # between keys or Blender will interpolate through the opposite rotation.
    if obj in previous and rotation.dot(previous[obj]) < 0:
        rotation.negate()
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = rotation
    previous[obj] = rotation
    key(obj, frame, "rotation_quaternion")


def animate_ferry(ferry, at_mooring=False, frame_end=154):
    """Count at the landing, then show the ferry already coasting in the chase."""
    previous = {}
    for frame in range(1, frame_end + 1):
        seconds = (frame - 1) / 24
        progress = (frame - 1) / (frame_end - 1)
        swell = sin(seconds * 2*pi/3.4)
        count = sum(sin(pi * (seconds - t) / .38) ** 2 for t in (2.36, 3.17)
                    if t < seconds < t + .38)
        launch = min(1, max(0, (seconds - 4.65) / 1.7))
        stroke = sin(2 * pi * launch) if at_mooring else 0
        dip = sin(pi * launch) ** 2 if at_mooring else 0

        ferry.root.location = (-2.6 - .55*launch**2 if at_mooring else -4.65 - 4.8*progress,
                               -3.8 - .23*launch**2 if at_mooring else -4.55 - 2.7*progress,
                               .045 + .017*swell)
        ferry.root.rotation_euler = (.009*swell, -.005*cos(seconds), .10 + .12*progress)
        key(ferry.root, frame, "location", "rotation_euler")

        ferry.torso.rotation_euler = (.12 + .018*swell + .05*dip, 0, -.10 - .07*count - .08*stroke)
        ferry.head.rotation_euler = (.06 + .17*count if at_mooring else .06, 0,
                                     .91 + .17*count if at_mooring else .62)
        ferry.lantern.rotation_euler = (.05*swell, .038*sin(seconds * 1.85 + .8), 0)
        key(ferry.torso, frame, "rotation_euler")
        key(ferry.head, frame, "rotation_euler")
        key(ferry.lantern, frame, "rotation_euler")

        # One departure stroke. During the chase the oar stays above the river.
        direction = Vector((.10 + .20*stroke, .69 - .13*dip, -.52 - .39*dip)).normalized()
        aim(ferry.oar, direction, frame, previous)
        torso_orientation = ferry.torso.rotation_euler.to_matrix()
        for sign, spacing, shoulder_local, (upper, forearm, _) in zip(
                (-1, 1), HAND_GRIPS, ferry.shoulders, ferry.arms):
            shoulder = ferry.torso.location + torso_orientation @ shoulder_local
            hand_position = ferry.oar.location + direction*spacing
            elbow = elbow_for(shoulder, hand_position, sign)
            for limb, start, end, width in ((upper, shoulder, elbow, .101),
                                            (forearm, elbow, hand_position, .090)):
                length = end - start
                limb.location = (start + end) / 2
                limb.scale = (width, width, length.length)
                aim(limb, length, frame, previous)
                key(limb, frame, "location", "scale")

    for obj in (ferry.root, ferry.torso, ferry.head, ferry.lantern, *previous):
        for curve in obj.animation_data.action.fcurves:
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
