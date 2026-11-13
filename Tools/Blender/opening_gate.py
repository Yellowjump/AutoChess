"""Build the dawn pass and its animated reveal for the opening film."""

import math
import random

import bpy
from mathutils import Vector


def build_gate(start_frame=1, end_frame=168):
    rng = random.Random(409)
    collection = bpy.data.collections.new("古修石门 | Dawn Pass")
    bpy.context.scene.collection.children.link(collection)
    center_x = 110

    def material(name, rgb, emission=0):
        value = bpy.data.materials.new(name)
        value.diffuse_color = (*rgb, 1)
        value.use_nodes = True
        surface = value.node_tree.nodes.get("Principled BSDF")
        surface.inputs["Base Color"].default_value = (*rgb, 1)
        surface.inputs["Roughness"].default_value = .91
        surface.inputs["Emission Color"].default_value = (*rgb, 1)
        surface.inputs["Emission Strength"].default_value = emission
        return value

    ground = material("Pass | Ochre slate", (.21, .26, .26))
    path = material("Pass | Worn stone trail", (.44, .43, .36))
    slate = [material("Pass | Stone " + str(i), c) for i, c in enumerate([
        (.26, .31, .30), (.34, .37, .34), (.32, .34, .31),
        (.44, .45, .39), (.29, .33, .33),
    ])]
    roof = material("Pass | Roof tiles", (.078, .118, .128))
    roof_detail = material("Pass | Roof tile edges", (.15, .20, .20))
    wood = material("Pass | Incense-dark timber", (.16, .117, .094))
    rust = material("Pass | Faded ribbon", (.41, .135, .073))
    vegetation = material("Pass | Pine needles", (.094, .16, .143))
    ridge_colors = [
        (.26, .34, .36), (.33, .42, .45), (.44, .52, .54),
        (.54, .60, .61), (.64, .66, .64), (.72, .69, .63), (.78, .71, .64),
    ]
    ridges = [material("Pass | Distant mist " + str(i), color, .45)
              for i, color in enumerate(ridge_colors)]
    sky = material("Pass | Dawn sky", (.87, .78, .67), .85)
    sun_haze = material("Pass | Sun haze", (1, .81, .61), 1)
    sun = material("Pass | Dawn sun", (1, .92, .75), 1)

    def mesh(name, verts, faces, shade):
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts, [], faces)
        data.materials.append(shade)
        data.update()
        obj = bpy.data.objects.new(name, data)
        collection.objects.link(obj)
        obj.location.x = center_x
        return obj

    def cube(name, x, y, z, width, depth, height, shade, bevel=.03):
        x1, x2 = x - width / 2, x + width / 2
        y1, y2 = y - depth / 2, y + depth / 2
        z1, z2 = z - height / 2, z + height / 2
        obj = mesh(name, [(x1, y1, z1), (x2, y1, z1), (x2, y2, z1), (x1, y2, z1),
                         (x1, y1, z2), (x2, y1, z2), (x2, y2, z2), (x1, y2, z2)],
                   [(0, 3, 2, 1), (0, 1, 5, 4), (1, 2, 6, 5),
                    (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)], shade)
        if bevel:
            mod = obj.modifiers.new("Chipped edges", "BEVEL")
            mod.width = bevel
            mod.segments = 1
        return obj

    def ground_height(x, y):
        mound = .59 * math.exp(-((x * .18)**2 + ((y - 3) * .32)**2))
        edge = .22 * math.sin(.43 * x + .19 * y) + .14 * math.sin(.63 * y)
        return mound + edge - .08

    columns, rows = 47, 52
    vertices = []
    for row in range(rows):
        y = -13 + row * .68
        for column in range(columns):
            x = (column - columns // 2) * .76
            vertices.append((x, y, ground_height(x, y) + rng.uniform(-.04, .04)))
    faces = []
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.extend(((a, a + 1, a + 1 + columns), (a, a + 1 + columns, a + columns)))
    terrain = mesh("Rocky pass", vertices, faces, ground)
    for shade in slate:
        terrain.data.materials.append(shade)
    for polygon in terrain.data.polygons:
        if rng.random() < .12:
            polygon.material_index = rng.choice((1, 5))

    def trail(samples):
        vertices = []
        for (x1, y1, width1), (x2, y2, width2) in zip(samples, samples[1:]):
            for i in range(7):
                t = i / 7
                x = x1 + (x2 - x1) * t
                y = y1 + (y2 - y1) * t
                width = width1 + (width2 - width1) * t
                for side in (-1, 1):
                    xx = x + width * side * rng.uniform(.46, .53)
                    vertices.append((xx, y, ground_height(xx, y) + .057))
        x, y, width = samples[-1]
        vertices.extend((xx, y, ground_height(xx, y) + .057) for xx in (x - width / 2, x + width / 2))
        return mesh("Worn footpath", vertices,
                    [(i, i + 1, i + 3, i + 2) for i in range(0, len(vertices) - 2, 2)], path)

    trail([(-5.2, -11, 1.75), (-4.6, -5.7, 1.55), (-4.35, -3.7, 1.5),
           (-3.2, -2.05, 1.5), (-1.4, -.72, 1.5), (0, .2, 1.55)])
    trail([(5.3, -11, 1.50), (3.8, -5.6, 1.35), (3.9, -3.25, 1.35),
           (2.75, -1.7, 1.4), (1.25, -.65, 1.45), (0, .2, 1.55)])
    trail([(0, 3.8, 1.5), (.2, 5.2, 1.35), (1.4, 8.3, 1.20), (2.8, 13.8, .9)])

    gate_y = 2.8
    base_z = .79
    for i in range(7):
        cube("Stone stair", 0, .22 + i * .31, .27 + i * .076, 3.18 - i * .066,
             .60, .34, slate[i % len(slate)])
    cube("Foundation", 0, gate_y, base_z - .16, 4.42, 1.58, .39, slate[1], .10)
    cube("Threshold", 0, gate_y, base_z + .02, 3.85, 1.20, .17, slate[3], .055)
    for sign in (-1, 1):
        x = sign * 1.41
        cube("Pier plinth", x, gate_y, base_z + .22, 1.06, 1.11, .35, slate[2], .065)
        cube("Pier foot", x, gate_y, base_z + .46, .86, .86, .22, slate[3], .055)
        for course in range(6):
            shift = rng.uniform(-.018, .018)
            cube("Pier course", x + shift, gate_y, base_z + .68 + course * .34,
                 .68, .68, .365, slate[(course + 2) % len(slate)], .035)
        cube("Pier capital", x, gate_y, base_z + 2.58, .94, .83, .21, slate[3], .048)
        cube("Bracket", x, gate_y, base_z + 2.77, 1.14, .95, .21, wood, .047)

    # Tapered blocks frame the arch and leave a clear gap through to the distant trail.
    for i in range(11):
        a = i * math.pi / 11 + .008
        b = (i + 1) * math.pi / 11 - .008
        points = [(radius * math.cos(theta), base_z + 1.69 + radius * math.sin(theta))
                  for radius, theta in [(1.07, a), (1.43, a), (1.43, b), (1.07, b)]]
        mesh("Stone arch voussoir", [(x, gate_y + y, z) for y in (-.41, .41) for x, z in points],
             [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2),
              (2, 6, 7, 3), (3, 7, 4, 0)], slate[3 if i == 5 else i % len(slate)])
    cube("Lintel", 0, gate_y, base_z + 3.14, 3.86, .94, .26, slate[2], .052)
    cube("Roof ledger", 0, gate_y, base_z + 3.35, 4.46, 1.12, .20, wood, .042)

    def roof_top(x, y):
        upturned = .52 * (abs(x) / 2.75)**4
        pitch = .57 * (1 - (abs(y) / 1.02)**.72)
        return base_z + 3.47 + pitch + upturned

    # The upward curve of the corners holds up in silhouette.
    roof_columns = 21
    ys = [-1.02, -.78, -.52, -.26, 0, .26, .52, .78, 1.02]
    verts = [(x, gate_y + y, roof_top(x, y)) for y in ys
             for x in [-2.75 + j * 5.50 / (roof_columns - 1) for j in range(roof_columns)]]
    faces = []
    for j in range(len(ys) - 1):
        for i in range(roof_columns - 1):
            a = j * roof_columns + i
            faces.append((a, a + 1, a + roof_columns + 1, a + roof_columns))
    tiles = mesh("Swept tiled roof", verts, faces, roof)
    thickness = tiles.modifiers.new("Roof fascia", "SOLIDIFY")
    thickness.thickness = .12

    def stroke(name, coordinates, radius, shade):
        data = bpy.data.curves.new(name, "CURVE")
        data.dimensions = "3D"
        data.resolution_u = 1
        data.bevel_depth = radius
        data.bevel_resolution = 1
        data.materials.append(shade)
        spline = data.splines.new("POLY")
        spline.points.add(len(coordinates) - 1)
        for point, coordinate in zip(spline.points, coordinates):
            point.co = (*coordinate, 1)
        obj = bpy.data.objects.new(name, data)
        collection.objects.link(obj)
        obj.location.x = center_x
        return obj

    for y in (-1.025, 0, 1.025):
        stroke("Upturned roof ridge", [(x, gate_y + y, roof_top(x, y) + .015)
               for x in [-2.75 + .25 * i for i in range(23)]], .068, roof_detail)
    for j in range(1, 23):
        x = -2.75 + j * 5.5 / 23
        stroke("Roof rib", [(x, gate_y + y, roof_top(x, y) + .016)
               for y in ys], .025, roof_detail)

    # Flattened distant silhouettes, progressively colored into the dawn haze.
    for layer, shade in enumerate(ridges):
        y = 15 + layer * 5.2
        crest = []
        for i in range(69):
            x = -48 + i * 1.4
            slope = abs(math.sin(x * (.16 - layer * .006) + layer * 2.19))
            jagged = .36 * math.sin(.59 * x + layer * 1.8) + .16 * math.sin(1.32 * x)
            crest.append((x, y, -.02 - layer * .46 + 2.42 * slope**2 + jagged))
        for i in range(7):
            crest[30 + i] = (crest[30 + i][0], y, crest[30 + i][2] - (1 - abs(i - 3) / 4) * (2.2 - .15 * layer))
        vertices = [point for crest_point in crest for point in (crest_point, (crest_point[0], y, -5))]
        mesh("Mountain silhouette " + str(layer + 1), vertices,
             [(i, i + 1, i + 3, i + 2) for i in range(0, len(vertices) - 2, 2)], shade)
    mesh("Warm horizon", [(-85, 56, -8), (85, 56, -8), (85, 56, 40), (-85, 56, 40)],
         [(0, 1, 2, 3)], sky)
    for y, radius, shade in [(54.1, 1.27, sun_haze), (54, 1.14, sun)]:
        disk = [(4.65 + radius * math.cos(theta), y, .57 + radius * math.sin(theta))
                for theta in [i * math.tau / 36 for i in range(36)]]
        mesh("Pale sun", disk, [tuple(range(36))], shade)

    # Rock clusters and crooked little pines keep the edge of the frame darker than the gate.
    def rock(x, y, radius):
        z = ground_height(x, y)
        angles = [i * math.tau / 7 for i in range(7)]
        vertices = [(x + radius * math.cos(angle) * rng.uniform(.83, 1.24),
                     y + radius * math.sin(angle) * .65, z - .14) for angle in angles]
        vertices += [(x - radius * .22 + radius * math.cos(angle) * .49,
                      y + radius * math.sin(angle) * .46,
                      z + radius * rng.uniform(.36, .83)) for angle in angles]
        faces = [(i, (i + 1) % 7, (i + 1) % 7 + 7, i + 7) for i in range(7)]
        faces.append(tuple(range(7, 14)))
        return mesh("Split shale", vertices, faces, rng.choice(slate))

    for side in (-1, 1):
        for _ in range(24):
            y = rng.uniform(-9, 7.5)
            x = side * rng.uniform(5.6 if y < -4 else 3.4, 10)
            rock(x, y, rng.uniform(.23, 1.1))
    for x, y, height in [(-5.55, -.7, 2.65), (-7.75, 5.7, 2.0), (5.4, 5, 2.10), (7.95, -.1, 3.25)]:
        z = ground_height(x, y)
        bend = math.copysign(.5, x)
        stroke("Pine trunk", [(x, y, z - .05), (x + bend * .6, y, z + height * .56),
                              (x + bend, y + .1, z + height)], .075, wood)
        for n, side in enumerate((-1, 1, -1, 1)):
            high = z + height * (.52 + .12 * n)
            target_x = x + bend * .5 + side * (.85 - .13 * n)
            stroke("Pine branch", [(x + bend * .55, y, high), (target_x, y - .03, high + .29)], .037, wood)
            vertices = [(target_x + dx, y + dy, high + dz) for dx, dy, dz in
                        [(-.77, 0, .29), (-.22, -.40, .26), (.70, -.1, .28),
                         (.4, .28, .25), (-.18, .47, .3), (0, 0, .63)]]
            mesh("Pine canopy", vertices, [(i, (i + 1) % 5, 5) for i in range(5)], vegetation)

    stake_x, stake_y = -3.16, -4.05
    stake_z = ground_height(stake_x, stake_y)
    stroke("Wayside stake", [(stake_x, stake_y, stake_z), (stake_x - .12, stake_y, stake_z + 1.11)], .068, wood)
    strip = mesh("Faded ribbon", [(stake_x - .11, stake_y - .02, stake_z + .86),
                                 (stake_x - .11, stake_y - .05, stake_z + .76),
                                 (stake_x + .18, stake_y - .09, stake_z + .64),
                                 (stake_x + .19, stake_y - .08, stake_z + .55),
                                 (stake_x + .60, stake_y - .18, stake_z + .50),
                                 (stake_x + .56, stake_y - .15, stake_z + .40)],
                 [(0, 1, 3, 2), (2, 3, 5, 4)], rust)
    fabric = strip.modifiers.new("Double-sided fabric", "SOLIDIFY")
    fabric.thickness = .008

    data = bpy.data.cameras.new("Stone Gate Reveal")
    camera = bpy.data.objects.new("Stone Gate Reveal", data)
    collection.objects.link(camera)
    data.type = "ORTHO"
    data.clip_end = 100
    for frame, position, target, width in [
        (start_frame, (-.39, -13.8, 4.48), (0, 3.5, 2.12), 11.75),
        (end_frame, (.47, -14.9, 5.38), (0, 3.2, 2.14), 16.15),
    ]:
        camera.location = (center_x + position[0], *position[1:])
        camera.rotation_euler = (Vector((center_x + target[0], *target[1:])) -
                                 camera.location).to_track_quat("-Z", "Y").to_euler()
        data.ortho_scale = width
        camera.keyframe_insert(data_path="location", frame=frame)
        camera.keyframe_insert(data_path="rotation_euler", frame=frame)
        data.keyframe_insert(data_path="ortho_scale", frame=frame)
    # The distant silhouette meshes should not cast solid geometric shadows through the haze.
    if bpy.context.scene.render.engine == "BLENDER_WORKBENCH":
        bpy.context.scene.display.shading.show_shadows = False
    return camera
