"""Articulated child for the Qinghe landing shot. The rest pose faces -Y."""

from dataclasses import dataclass
from math import acos, atan2, cos, pi, radians, sin, sqrt

import bpy
from mathutils import Vector


@dataclass
class Limb:
    upper: bpy.types.Object
    lower: bpy.types.Object
    tip: bpy.types.Object


@dataclass
class Child:
    root: bpy.types.Object
    torso: bpy.types.Object
    head: bpy.types.Object
    skirt: bpy.types.Object
    hair_tie: bpy.types.Object
    mouth: bpy.types.Object
    arms: tuple[Limb, Limb]
    legs: tuple[Limb, Limb]


def _cloth(name, color, grain=.16):
    material = bpy.data.materials.new("Child / " + name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    tree = material.node_tree
    shader = tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .91
    noise = tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 82
    noise.inputs["Detail"].default_value = 2
    tone = tree.nodes.new("ShaderNodeValToRGB")
    for element, factor in zip(tone.color_ramp.elements, (1 - grain, 1 + grain)):
        element.color = (*(component * factor for component in color), 1)
    tree.links.new(noise.outputs["Fac"], tone.inputs["Fac"])
    tree.links.new(tone.outputs["Color"], shader.inputs["Base Color"])
    return material


def _joint(name, parent=None, location=(0, 0, 0)):
    joint = bpy.data.objects.new("Child / " + name, None)
    bpy.context.collection.objects.link(joint)
    joint.parent = parent
    joint.location = location
    joint.empty_display_type = "PLAIN_AXES"
    joint.empty_display_size = .045
    return joint


def _mesh(name, vertices, faces, parent, material):
    mesh = bpy.data.meshes.new("Child / " + name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(material)
    return obj


def _rings(name, parent, levels, material, sides=16, smooth=False):
    vertices = []
    for z, rx, ry, cy in levels:
        for i in range(sides):
            angle = 2 * pi * (i + .5) / sides
            vertices.append((rx * cos(angle), cy + ry * sin(angle), z))
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(levels) - 1):
        for i in range(sides):
            a, b = j * sides + i, j * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range((len(levels) - 1) * sides, len(levels) * sides)))
    obj = _mesh(name, vertices, faces, parent, material)
    if smooth:
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) == 4
    return obj


def _ellipsoid(name, parent, location, size, material, smooth=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1)
    obj = bpy.context.object
    obj.name = "Child / " + name
    obj.parent = parent
    obj.location = location
    obj.scale = size
    obj.data.materials.append(material)
    if smooth:
        for face in obj.data.polygons:
            face.use_smooth = True
    return obj


def _thread(name, parent, points, radius, material):
    curve = bpy.data.curves.new("Child / " + name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 8
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(curve.name, curve)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(material)
    return obj


THIGH = .325
SHIN = .315
ANKLE = .062
RUN_CYCLE = 19.2  # RUN00_F: 24 source frames at 30 fps, resampled to 24 fps.
RUN_ARM_POSES = (
    ((41, 50, 59, 49, 26, 3, -13, 0, 41), (-101, -63, -20, -39, -73, -88, -95, -100, -101)),
    ((44, 5, -18, -11, 16, 43, 62, 58, 44), (-106, -99, -85, -80, -74, -61, -42, -61, -106)),
)


def _leg(root, side, pants, sock, shoe):
    hip = _joint("Hip", root, (side * .099, 0, .64))
    _rings("Trouser thigh", hip, [(-.35, .068, .061, 0), (-.24, .087, .070, 0),
        (-.10, .095, .080, 0), (.05, .093, .075, 0)], pants, smooth=True)
    knee = _joint("Knee", hip, (0, 0, -THIGH))
    _rings("Trouser calf", knee, [(-.28, .044, .046, 0), (-.20, .057, .054, 0),
        (-.08, .070, .061, 0), (.04, .064, .058, 0)], pants, smooth=True)
    _rings("Cloth boot cuff", knee, [(-.309, .043, .046, 0), (-.22, .049, .049, 0)],
        sock, sides=8)
    foot = _joint("Foot", knee, (0, 0, -SHIN))
    _ellipsoid("Cloth shoe", foot, (0, -.050, -.028), (.057, .104, .035), shoe)
    _ellipsoid("Stitched sole", foot, (0, -.048, -.054), (.058, .107, .012), sock)
    return Limb(hip, knee, foot)


def _arm(torso, side, fabric, trim, skin):
    shoulder = _joint("Shoulder", torso, (side * .203, -.002, .382))
    _rings("Upper sleeve", shoulder, [(-.223, .067, .068, 0), (-.137, .084, .079, 0),
        (-.02, .095, .093, 0), (.065, .064, .064, 0)], fabric, smooth=True)
    elbow = _joint("Elbow", shoulder, (0, 0, -.209))
    _rings("Lower sleeve", elbow, [(-.182, .058, .060, -.004), (-.126, .072, .073, -.004),
        (-.025, .071, .071, 0), (.045, .061, .061, 0)], fabric, smooth=True)
    _rings("Sleeve facing", elbow, [(-.18, .059, .061, -.004), (-.146, .064, .064, -.004)],
        trim, smooth=True)
    wrist = _joint("Wrist", elbow, (0, 0, -.173))
    _ellipsoid("Small hand", wrist, (0, -.008, -.043), (.037, .026, .055), skin)
    thumb = _ellipsoid("Thumb", wrist, (-side * .026, -.021, -.032), (.019, .020, .027), skin)
    thumb.rotation_euler.y = side * .29
    return Limb(shoulder, elbow, wrist)


def build_child():
    """Create the child and return its transform hierarchy."""
    root = _joint("Root")
    body = _joint("Torso", root, (0, 0, .64))
    indigo = _cloth("Indigo cotton", (.066, .106, .151))
    sleeve = _cloth("Faded indigo sleeves", (.085, .133, .177))
    shade = _cloth("Robe fold", (.046, .071, .108))
    hem = _cloth("Overdyed edges", (.116, .160, .184))
    pants = _cloth("Trousers", (.113, .127, .137))
    binding = _cloth("Natural linen", (.31, .269, .196))
    sash = _cloth("Faded woven sash", (.176, .121, .079))
    skin = _cloth("Warm clay complexion", (.43, .263, .168), .07)
    hair = _cloth("Ink hair", (.017, .019, .024))
    seam = _cloth("Hemp stitching", (.20, .184, .151))
    cinnabar = _cloth("Faded cinnabar ribbon", (.235, .074, .042))

    _rings("Cross-collared tunic", body, [(0, .162, .115, 0), (.115, .166, .119, 0),
        (.25, .199, .120, 0), (.373, .222, .113, 0), (.424, .182, .099, .006),
        (.451, .105, .087, .003)], indigo, smooth=True)
    _rings("Collar band", body, [(.407, .113, .104, -.009), (.451, .108, .089, .001),
        (.467, .085, .078, .004)], binding, smooth=True)
    _thread("Right lapel", body, [(.097, -.087, .44), (.043, -.114, .37), (-.077, -.120, .259),
        (-.147, -.084, .157)], .012, hem)
    _thread("Left lapel", body, [(-.101, -.079, .444), (-.048, -.115, .375), (.011, -.122, .322)],
        .011, shade)
    _thread("Back neck seam", body, [(-.13, .077, .415), (0, .107, .398), (.13, .077, .415)], .003, hem)
    _thread("Back center seam", body, [(0, .107, .398), (.003, .124, .25), (0, .119, .13)],
        .0025, hem)
    _rings("Flat sash", body, [(.085, .171, .123, 0), (.14, .173, .125, 0)], sash, smooth=True)
    _thread("Sash cord", body, [(-.147, .067, .10), (0, .128, .105), (.148, .069, .108)], .005, seam)
    _ellipsoid("Sash knot", body, (.096, .112, .107), (.046, .022, .030), sash)
    _thread("Short sash tail", body, [(.103, .125, .113), (.117, .136, .055), (.094, .155, -.020)],
        .012, sash)
    skirt = _joint("Robe hem", body, (0, 0, .09))
    _rings("Skirt of travelling robe", skirt, [(-.368, .239, .159, .025),
        (-.318, .234, .154, .022), (-.16, .205, .146, .015), (.016, .166, .119, 0)],
        indigo, smooth=True)
    _rings("Hem tape", skirt, [(-.367, .241, .160, .025), (-.347, .239, .159, .024)],
        shade, smooth=True)
    for sign in (-1, 1):
        _thread("Long robe fold", skirt, [(sign * .064, .119, -.01),
            (sign * .081, .157, -.16), (sign * .117, .171, -.34)], .005, shade)
    _thread("Hanging back fold", skirt, [(0, .12, 0), (.008, .155, -.16), (.020, .178, -.34)],
        .005, hem)

    head = _joint("Head", body, (0, -.008, .449))
    _rings("Neck", head, [(-.025, .054, .057, .003), (.122, .065, .062, .004)], skin, smooth=True)
    _rings("Child face", head, [(.059, .057, .071, -.023), (.090, .113, .094, -.008),
        (.167, .151, .119, -.001), (.248, .159, .125, .009), (.315, .132, .116, .016),
        (.346, .081, .080, .024)], skin, sides=12, smooth=True)
    for side in (-1, 1):
        _ellipsoid("Ear", head, (side * .147, .000, .164), (.037, .027, .049), skin)
        _thread("Eye", head, [(side * .048, -.113, .207), (side * .072, -.114, .210),
            (side * .092, -.102, .204)], .0048, hair)
        _thread("Brow", head, [(side * .043, -.112, .232), (side * .067, -.115, .235),
            (side * .090, -.106, .230)], .004, hair)
    _ellipsoid("Nose", head, (0, -.121, .169), (.026, .023, .031), skin)
    mouth = _ellipsoid("Mouth", head, (0, -.103, .125), (.030, .011, .005), sash)
    _ellipsoid("Hair cap", head, (0, .030, .262), (.166, .133, .119), hair)
    _ellipsoid("Hair at nape", head, (0, .098, .171), (.137, .068, .103), hair)
    _thread("Loose fringe", head, [(-.135, -.051, .239), (-.087, -.099, .279),
        (-.032, -.105, .261), (-.008, -.104, .254)], .011, hair)
    _thread("Short temple strand", head, [(.127, -.032, .259), (.150, -.037, .216),
        (.143, -.027, .182)], .010, hair)
    _ellipsoid("Tied topknot", head, (.006, .049, .383), (.067, .058, .074), hair)
    tie = _joint("Hair ribbon", head, (0, .092, .351))
    _thread("Ribbon knot", head, [(-.047, .071, .371), (.005, .099, .364), (.052, .074, .371)],
        .009, cinnabar)
    _thread("Ribbon end", tie, [(0, 0, 0), (.046, .048, -.054), (.059, .035, -.126)],
        .007, cinnabar)
    _thread("Fine ribbon end", tie, [(0, 0, 0), (-.029, .057, -.040), (-.047, .038, -.106)],
        .006, cinnabar)
    arms = tuple(_arm(body, side, sleeve, hem, skin) for side in (-1, 1))
    legs = tuple(_leg(root, side, pants, binding, hair) for side in (-1, 1))
    return Child(root, body, head, skirt, tie, mouth, arms, legs)


def _smooth(t):
    return t * t * (3 - 2 * t)


def _value(frame, keys, eased=True):
    if frame <= keys[0][0]:
        return keys[0][1]
    for (start, a), (end, b) in zip(keys, keys[1:]):
        if frame <= end:
            t = (frame - start) / (end - start)
            return a + (b - a) * (_smooth(t) if eased else t)
    return keys[-1][1]


def chase_position(frame):
    """Landing-space position. Tangents are in scene units per frame."""
    poses = [(1, 3.20, -.053), (12, 2.64, -.063), (27, 1.59, -.073),
             (37, .92, -.055), (46, .65, 0)]
    for (start, a, speed_a), (end, b, speed_b) in zip(poses, poses[1:]):
        if frame <= end:
            length = end - start
            t = max(0, (frame - start) / length)
            u = 1 - t
            return a * (1 + 2 * t) * u * u + speed_a * length * t * u * u + \
                b * t * t * (3 - 2 * t) - speed_b * length * u * t * t
    return poses[-1][1]


def _run_pose(samples, phase):
    sample = phase % 1 * (len(samples) - 1)
    index = int(sample)
    return samples[index] + (samples[index + 1] - samples[index]) * _smooth(sample - index)


def _foot(frame, steps):
    start_y = steps[0][2]
    for lift, land, target_y, height in steps[1:]:
        if frame <= lift:
            break
        if frame < land:
            t = (frame - lift) / (land - lift)
            return (
                start_y + (target_y - start_y) * _smooth(t),
                ANKLE + height * sin(pi * t),
                radians(21) * sin(2 * pi * t),
            )
        start_y = target_y
    return start_y, ANKLE, 0


def _pose_leg(limb, y, hip_height, foot, side, frame):
    foot_y, foot_z, foot_pitch = foot
    dy = foot_y - y
    dz = hip_height - foot_z
    distance = min(THIGH + SHIN - .0001, max(.15, sqrt(dy * dy + dz * dz)))
    offset = acos((THIGH * THIGH + distance * distance - SHIN * SHIN) / (2 * THIGH * distance))
    angle = atan2(dy, dz) - offset
    knee = acos((distance * distance - THIGH * THIGH - SHIN * SHIN) / (2 * THIGH * SHIN))
    limb.upper.location.z = hip_height
    limb.upper.rotation_euler.x = angle
    limb.lower.rotation_euler.x = knee
    limb.tip.rotation_euler.x = foot_pitch - angle - knee
    limb.tip.rotation_euler.z = side * radians(5)
    limb.upper.keyframe_insert("location", index=2, frame=frame)
    for joint in (limb.upper, limb.lower, limb.tip):
        joint.keyframe_insert("rotation_euler", frame=frame)


def animate_child(child):
    """Animate a run, a two-step stop, an outstretched hand and a quiet release."""
    left_steps = [(1, 1, 3.09, 0), (3, 12, 2.43, .11), (21, 31, 1.24, .13),
        (38, 45, .71, .06)]
    right_steps = [(1, 1, 3.27, 0), (1, 4, 2.97, .09), (12, 22, 1.86, .13),
        (31, 40, .64, .085)]
    for frame in range(1, 141):
        y = chase_position(frame)
        left = _foot(frame, left_steps)
        right = _foot(frame, right_steps)
        run = _value(frame, [(1, .72), (10, 1), (29, 1), (35, .71), (46, 0)])
        cycle = (frame - 3) / RUN_CYCLE
        phase = cycle * 2 * pi
        brake = _value(frame, [(32, 0), (39, 1), (46, .28), (53, 0)])
        breath = sin((frame - 49) * .14) * .006 * (1 - run)
        reach = _value(frame, [(49, 0), (56, .20), (65, 1), (74, .92), (82, 1),
            (91, .88), (97, .41), (109, .03), (119, 0)])
        hip_height = .649 - .023 * run * cos(2 * phase) - .021 * brake + breath
        for ankle_y, ankle_z, _ in (left, right):
            max_extension = THIGH + SHIN - .005
            max_height = ankle_z + sqrt(max(.11, max_extension * max_extension - (ankle_y - y) ** 2))
            hip_height = min(hip_height, max_height)

        child.root.location.y = y
        child.root.location.x = .021 * run * sin(phase)
        child.root.keyframe_insert("location", index=1, frame=frame)
        child.root.keyframe_insert("location", index=0, frame=frame)
        child.torso.location.z = hip_height
        child.torso.rotation_euler = (
            radians(17 * run + 7 * reach + 8 * brake) + .025 * sin(2 * phase) * run,
            .045 * sin(phase) * run - .025 * reach,
            .095 * sin(phase) * run - .075 * reach,
        )
        child.head.rotation_euler = (
            -.15 * run - .22 * reach + .10 * _value(frame, [(95, 0), (112, 1), (140, .65)]),
            .055 * cos(phase) * run - .045 * reach,
            -.14 * reach + .05 * sin(frame * .13),
        )
        child.skirt.rotation_euler = (.18 * run + .08 * sin(phase) * run - .095 * reach, 0,
            -.07 * sin(phase) * run)
        child.hair_tie.rotation_euler = (.28 * run + .18 * sin(frame * .27),
            .05 * sin(frame * .17), .18 * sin(frame * .21))
        calling = _value(frame, [(56, 0), (62, 1), (70, .5), (73, .75), (81, 0)])
        child.mouth.scale.z = .005 + .018 * calling
        child.mouth.keyframe_insert("scale", index=2, frame=frame)
        child.torso.keyframe_insert("location", index=2, frame=frame)
        for joint in (child.torso, child.head, child.skirt, child.hair_tie):
            joint.keyframe_insert("rotation_euler", frame=frame)

        for side, arm, (upper_pitch, elbow_flex) in zip((-1, 1), child.arms, RUN_ARM_POSES):
            right_arm = side == 1
            swing = radians(_run_pose(upper_pitch, cycle) - 19) * .8 * run
            bend = radians(_run_pose(elbow_flex, cycle)) * .68 * run
            arm.upper.rotation_euler = (
                -.06 + swing - .21 * brake - (1.39 if right_arm else .34) * reach,
                side * -.16 - (.15 if right_arm else 0) * reach,
                -.11 * side * run + (.15 if right_arm else .08) * reach,
            )
            arm.lower.rotation_euler.x = -.16 * (1 - run) + bend - (.21 if right_arm else .77) * reach
            arm.tip.rotation_euler.x = .08 + .20 * reach
            for joint in (arm.upper, arm.lower, arm.tip):
                joint.keyframe_insert("rotation_euler", frame=frame)

        _pose_leg(child.legs[0], y, hip_height, left, -1, frame)
        _pose_leg(child.legs[1], y, hip_height, right, 1, frame)
