"""Build and render the animated shots for The Twelfth Name in Blender 4.1."""

import argparse
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(HERE))


def color(hex_rgb):
    rgb = [int(hex_rgb[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x < .04045 else ((x + .055) / 1.055) ** 2.4 for x in rgb)


def material(name, hex_rgb):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color(hex_rgb), 1)
    return mat


def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def box(name, xyz, size, mat, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz, rotation=(0, 0, angle))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    obj.data.materials.append(mat)
    return obj


def stroke(name, points, radius, mat):
    data = bpy.data.curves.new(name, "CURVE")
    data.dimensions = '3D'
    data.bevel_resolution = 0
    data.bevel_depth = radius
    spline = data.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for point, xyz in zip(spline.points, points):
        point.co = (*xyz, 1)
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def camera(name, poses, lens):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    data.clip_end = 180
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for frame, location, look_at in poses:
        obj.location = location
        obj.rotation_euler = (Vector(look_at) - obj.location).to_track_quat('-Z', 'Y').to_euler()
        obj.keyframe_insert('location', frame=frame)
        obj.keyframe_insert('rotation_euler', frame=frame)
    return obj


def ridge(y, peak, palette):
    verts = []
    faces = []
    for i in range(350):
        x = -115 + i * .66
        height = (.23 + .42 * abs(math.sin(x * .074 + y)) ** 3 +
                  .26 * abs(math.cos(x * .16 + y * .29)) ** 3 +
                  .077 * math.sin(x * .73 + y) + .019 * math.sin(x * 2.17)) * peak
        verts.extend([(x, y, height), (x, y, -1.2)])
        if i:
            j = i * 2
            faces.append((j - 2, j - 1, j + 1, j))
    mesh("Layered ink ridge", verts, faces, palette)


def far_bank(rng):
    bank = material("Distant bank wash", '68776b')
    leaves = material("Distant pine wash", '526156')
    y = -14.5
    for i in range(37):
        x = -32 + 1.25 * i
        if -13 < x < 2 and rng.random() < .56:
            continue
        z = rng.uniform(.11, .34) + .19 * abs(math.sin(.28 * x))
        width = rng.uniform(.6, 1.65)
        crest = [(x - width, y, .03), (x - width * .66, y, z * .65), (x - width * .23, y, z),
                 (x + width * .34, y, z * .92), (x + width, y, .03)]
        mesh("Broken river bank", crest, [tuple(range(len(crest)))], bank)
        if i % 2:
            continue
        h = rng.uniform(.42, 1.3)
        bend = rng.uniform(-.27, .27)
        stroke("Ink pine trunk", [(x, y - .06, z * .55), (x + bend * .48, y - .07, z + h * .49),
               (x + bend, y - .09, z + h)], .019, bank)
        for level in (.48, .68, .86):
            tip = x + bend * level
            height = z + h * level
            spread = h * (1 - level) * .93
            vertices = [(tip - spread, y - .095, height), (tip - spread * .48, y - .095, height + .12 * h),
                        (tip + .08 * h, y - .095, height + .18 * h),
                        (tip + spread, y - .095, height + .016 * h), (tip, y - .095, height - .05 * h)]
            mesh("Broken pine crown", vertices, [(0, 1, 2, 3, 4)], leaves)


def build_river():
    rng = random.Random(417)
    plank = [material("Wet wood " + str(i), code) for i, code in enumerate(
        ["5b5049", "6e6051", "776554", "5d5146", "82705a", "65594c"])]
    post = material("Dock posts", "4a4039")
    rope = material("Mooring fibre", "aa9475")
    river = material("River", "87939a")
    water = [material("River strokes " + str(i), code) for i, code in enumerate(
        ["99a4a5", "aab0aa", "78898e", "909d9f"])]
    box("Qinghe river", (0, -23, -.08), (125, 90, .12), river)
    for i in range(21):
        y = .28 + i * .29
        box("Landing plank %02d" % i, (rng.uniform(-.06, .04), y, .28),
            (rng.uniform(1.8, 2.03), rng.uniform(.255, .281), .16),
            plank[i % len(plank)], rng.uniform(-.022, .022))
        if i % 3 == 0:
            for sign in (-1, 1):
                x = sign * rng.uniform(.73, .86)
                stroke("Plank grain", [(x - .12, y, .362), (x + .12, y+.012, .362)], .011, post)
    for x in (-.83, .83):
        box("Landing joist", (x, 3.2, .16), (.19, 6.3, .22), post)
        for y, height in ((.54, .62), (2.6, .56), (4.9, .64)):
            box("Mooring post", (x * 1.21, y, .13), (.19, .19, 1.1 + height * .18), post, .05)
            for z in (.53, .56, .59):
                stroke("Rope wrap", [(x*1.21+.127*math.cos(a*math.tau/10),
                                     y+.127*math.sin(a*math.tau/10), z) for a in range(11)], .018, rope)
    shore = material("Shale bank", "77736c")
    mesh("Rocky bank", [(-26, 5.5, .18), (2, 6.2, .24), (26, 7.4, .32),
        (26, 17, .39), (-26, 17, .30)], [(0, 1, 2, 3, 4)], shore)
    reeds = material("Reed stems", "504e43")
    for i in range(42):
        x = rng.choice((-1, 1)) * rng.uniform(1.2, 5.5)
        y = rng.uniform(4.7, 6.25)
        height = rng.uniform(.3, .94)
        stroke("Reed", [(x, y, .1), (x+.09, y+.018, height*.66), (x+.19, y+.05, height)], .012, reeds)
    for i in range(178):
        x, y = rng.uniform(-35, 26), rng.uniform(-27, 4.4)
        if -1.15 < x < 1.15 and y > 0:
            continue
        length = rng.uniform(.12, 1.35)
        line = stroke("Water ripple", [(x, y, .002), (x + length*.5, y+.045, .002),
                                        (x + length, y, .002)], rng.uniform(.005, .012), rng.choice(water))
        line.keyframe_insert('location', frame=1)
        line.location.x = -.55 - length * .3
        line.location.y = -.12
        line.keyframe_insert('location', frame=144)
        for curve in line.animation_data.action.fcurves:
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
    for i, (y, height, shade) in enumerate((
        (-48, 14.8, 'bbb9ad'), (-36, 10.9, 'a3a69e'), (-27, 7.2, '909891'), (-21, 4.3, '838e86'),
    )):
        ridge(y, height, material("Mist ridge %d" % i, shade))
    far_bank(rng)
    mesh("Ink-paper sky", [(-180, -63, -3), (180, -63, -3), (180, -63, 48), (-180, -63, 48)],
         [(0, 1, 2, 3)], material("Ink-paper sky", 'ccc0aa'))
    return water


def wakes(ferry_root, mats):
    for side in (-1, 1):
        for offset in range(3):
            x = 1.35 + offset * .22
            y = .36 + offset * .18
            obj = stroke("Ferry wake", [(x, side*y, .018), (x+.72, side*(y+.32), .018),
                                       (x+1.55, side*(y+.46), .018), (x+2.25, side*(y+.50), .018)],
                         .010 - offset * .002, mats[offset])
            obj.parent = ferry_root
            obj.scale = (.65, .92, 1)
            obj.keyframe_insert('scale', frame=1)
            obj.scale = (1.18, 1.27, 1)
            obj.keyframe_insert('scale', frame=144)


def build_river_shots(scene, shot):
    from opening_child import build_child, animate_child, chase_position
    from opening_ferry import build_ferry, animate_ferry

    water = build_river()
    ferry = build_ferry()
    scene.frame_end = 154
    animate_ferry(ferry, at_mooring=shot == 'ferry', frame_end=scene.frame_end)

    if shot == 'ferry':
        scene.camera = camera("01 / Roll call at the landing", [
            (1, (.9, 1.68, 2.38), (-2.33, -3.76, 1.05)),
            (154, (.66, 1.44, 2.26), (-2.31, -3.80, 1.04)),
        ], 39)
        return

    wakes(ferry.root, water)
    child = build_child()
    animate_child(child)
    child_parent = bpy.data.objects.new("Child on landing", None)
    bpy.context.collection.objects.link(child_parent)
    child_parent.location.z = .36
    child.root.parent = child_parent

    # One continuous camera follows the run, then widens as the child reaches out.
    chase_poses = []
    for frame in range(1, scene.frame_end + 1):
        y = chase_position(frame)
        draw_back = max(0, min(1, (frame - 42) / 112))
        draw_back = draw_back * draw_back * (3 - 2 * draw_back)
        chase_poses.append((frame, (.62 + .13 * draw_back, y + 1.95 + 2.9 * draw_back, 1.69 + .57 * draw_back),
                            (-.84 - 2.42 * draw_back, y - .73 - 1.3 * draw_back, 1.49 - .24 * draw_back)))
    scene.camera = camera("02 / Chase to the water's edge", chase_poses, 36)


def configure(scene, width, height):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 12
    scene.world.color = color('c7c5b9')
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.exposure = 0
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.fps = 24
    scene.frame_start = 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('shot', choices=('ferry', 'child', 'gate'))
    parser.add_argument('output', type=Path)
    parser.add_argument('--size', nargs=2, type=int, default=(1280, 604))
    parser.add_argument('--preview', type=int, help='Render a single PNG at this frame')
    parser.add_argument('--blend', type=Path, help='Save the editable source scene')
    parser.add_argument('--png-sequence', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.world = bpy.data.worlds.new("Mist")
    configure(scene, *args.size)
    if args.shot == 'gate':
        from opening_gate import build_gate
        scene.frame_end = 154
        scene.camera = build_gate(end_frame=scene.frame_end)
    else:
        build_river_shots(scene, args.shot)
    from opening_ink import apply_ink_washes, make_paper_composite
    apply_ink_washes()
    make_paper_composite(scene)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    if args.preview is not None or args.png_sequence:
        scene.render.image_settings.file_format = 'PNG'
        scene.render.image_settings.color_mode = 'RGB'
    else:
        scene.render.image_settings.file_format = 'FFMPEG'
        scene.render.ffmpeg.format = 'MPEG4'
        scene.render.ffmpeg.codec = 'H264'
        scene.render.ffmpeg.constant_rate_factor = 'HIGH'
        scene.render.ffmpeg.audio_codec = 'NONE'
    scene.render.filepath = str(args.output.resolve())
    scene.frame_set(args.preview if args.preview is not None else 1)
    if args.blend:
        args.blend.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(args.blend.resolve()))
    if args.preview is not None:
        bpy.ops.render.render(write_still=True)
    else:
        bpy.ops.render.render(animation=True)


if __name__ == '__main__':
    main()
