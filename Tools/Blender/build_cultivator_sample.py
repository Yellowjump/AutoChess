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
bpy.context.preferences.filepaths.save_version = 0
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


def palette_faces(obj, slots):
    uv = obj.data.uv_layers["Palette"]
    for polygon, slot in zip(obj.data.polygons, slots):
        for loop in polygon.loop_indices:
            uv.data[loop].uv = ((slot % 4 + .5) / 4, (slot // 4 + .5) / 4)


def shell(name, rows, bands, color):
    """Loft closed sections with palette changes on the actual surface."""
    n = len(rows[0])
    faces = [tuple(reversed(range(n)))]
    slots = [color]
    for j in range(len(rows) - 1):
        for i in range(n):
            k = (i + 1) % n
            faces.append((j*n+i, j*n+k, (j+1)*n+k, (j+1)*n+i))
            slots.append(bands[j])
    faces.append(tuple(range((len(rows)-1)*n, len(rows)*n)))
    slots.append(color)
    obj = mesh(name, [p for row in rows for p in row], faces, color)
    palette_faces(obj, slots)
    return obj


def ellipse(cx, cy, z, rx, ry, n=12):
    return [(cx + rx*math.cos(i*math.tau/n), cy + ry*math.sin(i*math.tau/n), z)
            for i in range(n)]


# Open the sides of the bodice and continue those exact edge loops into the sleeves.
# The second UV channel marks sleeve vertices for the humanoid rig fitter.
n = 12
broad = CHARACTER == "Chongshan"
long = CHARACTER in ("ShenYan", "Zhuxing")
hem = .48 if long else .63
levels = [(hem,.39,.245), (hem+.032,.386,.243), (.86,.29,.196), (.951,.241,.169),
          (.964,.25,.178), (1.047,.252,.18), (1.064,.247,.175), (1.245,.305,.194),
          (1.443,.33 if broad else .302,.174), (1.515,.135,.116)]
rows = []
for j, (z, rx, ry) in enumerate(levels):
    row = []
    for i, (x,y,h) in enumerate(ellipse(0,0,z,rx,ry,n)):
        if j < 3:
            fold = 1 + (.065 if i%2 else -.045) * (1 if j < 2 else .4)
            x, y = x*fold, y*fold
            h += (.038*math.sin(i*math.tau/n+.7) + (.045 if i == 9 else 0)) * (1 if j<2 else .2)
        row.append((x,y,h))
    rows.append(row)
verts = [v for row in rows for v in row]
faces, slots = [tuple(reversed(range(n)))], [6]
sleeve_vertices = set()
for j in range(len(rows)-1):
    for i in range(n):
        if j == 7 and i in (10,11,0,1,4,5,6,7):
            continue
        k = (i+1)%n
        faces.append((j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i))
        slots.append([8,6,6,10,10,10,6,6,7][j])
faces.append(tuple(range(9*n,10*n)))
slots.append(7)
for side, start in [(1,10),(-1,4)]:
    ids = [(start+k)%n for k in range(5)]
    rim = [7*n+i for i in ids] + [8*n+i for i in reversed(ids)]
    original = [verts[i] for i in rim]
    for cx,cy,z,rx,ry,slot in [(.345,-.012,1.355,.135,.154,7),(.416,-.039,1.16,.127,.146,7),
                               (.431,-.047,1.115,.138,.158,7),(.438,-.05,1.091,.132,.152,8)]:
        next_rim = []
        for _,oy,oz in original:
            angle = math.atan2((oz-1.344)/.099, oy/.158)
            radial = math.sin(angle)*rx
            next_rim.append(len(verts))
            sleeve_vertices.add(len(verts))
            verts.append((side*(cx+.85*radial), cy+math.cos(angle)*ry, z+.53*radial))
        for i,a in enumerate(rim):
            k=(i+1)%len(rim)
            faces.append((a,rim[k],next_rim[k],next_rim[i]))
            slots.append(slot)
        rim=next_rim
    faces.append(tuple(rim))
    slots.append(12)
garment = mesh("Short robe", verts, faces, 6)
palette_faces(garment, slots)
seam = garment.data.uv_layers.new(name="Sleeve fit")
for loop in garment.data.loops:
    seam.data[loop.index].uv = (1 if loop.vertex_index in sleeve_vertices else 0, .5)
garment.data.uv_layers["Palette"].active_render = True

def front_patch(name, points, color):
    # Detail follows the underlying cheek or cloth; orient it towards the viewer.
    outline = [Vector(p) for p in points]
    normal = (outline[1]-outline[0]).cross(outline[2]-outline[0])
    if normal.y > 0:
        outline.reverse()
    return mesh(name, outline, [tuple(range(len(outline)))], color)

for side in (-1,1):
    x=side*.16
    boot_rows = [ellipse(x,y,z,rx,ry,10) for z,rx,ry,y in
                 [(.012,.102,.182,-.060),(.045,.107,.19,-.066),(.105,.106,.19,-.07),
                  (.175,.096,.13,-.025),(.254,.098,.105,0),(.28,.102,.108,0),(.305,.099,.105,0)]]
    shell("Cloth boot", boot_rows, [12,4,4,4,8,8], 4)
    shell("Trouser", [ellipse(side*cx,0,z,w,d,10) for z,cx,w,d in
          [(.24,.16,.087,.09),(.34,.158,.099,.103),(.53,.147,.12,.12),(.70,.14,.142,.13),(.86,.136,.13,.12)]],
          [12]*4,12)
    wrist=(side*.49,-.15 if side == -1 else -.075,.93)
    elbow=(side*.43,-.045,1.12)
    segment("Forearm",wrist,elbow,.062,.084,1,9)
    segment("Wrist binding",wrist,Vector(wrist)+Vector((-side*.018,0,.085)),.073,.081,8,9)
    gem("Mitten hand",(wrist[0],wrist[1]-.013,.882),(.071,.065,.091),1,2)
    gem("Thumb",(wrist[0]-side*.05,wrist[1]-.04,.903),(.032,.038,.048),1,2)

# Bias-cut lapels follow the chest from the neck to the waist.
for side in (-1,1):
    path = [(side*.105,-.083,1.51),(side*.144,-.163,1.43),
            (side*.076,-.192,1.32),(-side*.018,-.195,1.24)]
    if side == -1:
        path += [(.103,-.180,1.14),(.172,-.139,1.05)]
    for a,b in zip(path,path[1:]):
        w=.021
        front_patch("Folded robe lapel",[(a[0]-w,a[1]-.008,a[2]),(a[0]+w,a[1]-.008,a[2]),
                    (b[0]+w,b[1]-.008,b[2]),(b[0]-w,b[1]-.008,b[2])],8)
gem("Belt knot",(.15,-.151,1.003),(.063,.043,.047),15,2)
segment("Neck",(0,0,1.44),(0,0,1.68),.094,.104,1,10)

# Nose bridge, tip, cheeks and chin are cut into a single facial surface.
face_levels=[(1.565,.077,.098,-.006,0),(1.626,.142,.144,-.012,0),
             (1.715,.202,.177,-.005,.005),(1.774,.223,.188,0,.014),
             (1.803,.229,.191,0,.066),(1.837,.236,.195,0,.049),
             (1.915,.239,.194,0,.019),(1.974,.243,.203,.005,.006),
             (2.065,.231,.197,.012,0),(2.136,.162,.155,.02,0)]
head_rows=[]
for z,rx,ry,cy,nose in face_levels:
    row=ellipse(0,cy,z,rx,ry,20)
    head_rows.append([(x,y-(nose if i==15 else 0),h) for i,(x,y,h) in enumerate(row)])
head = shell("Angular oversized head", head_rows, [1]*9, 1)
for face in head.data.polygons:
    face.use_smooth = True

def cheek_depth(x,z):
    def row_depth(row):
        front=row[15:]+row[:1]
        x_abs=abs(x)
        for a,b in zip(front,front[1:]):
            if x_abs <= b[0]:
                return a[1]+(b[1]-a[1])*(x_abs-a[0])/(b[0]-a[0])
        return front[-1][1]
    for a,b in zip(head_rows,head_rows[1:]):
        if z <= b[0][2]:
            t=(z-a[0][2])/(b[0][2]-a[0][2])
            return row_depth(a)*(1-t)+row_depth(b)*t
    return row_depth(head_rows[-1])

def face_mark(name, outline, slot, lift):
    front_patch(name,[(x,cheek_depth(x,z)-lift,z) for x,z in outline],slot)

for side in (-1,1):
    gem("Ear",(side*.223,.002,1.841),(.042,.045,.067),0,2)
    def mark(name, outline, slot, lift):
        face_mark(name,[(side*x,z) for x,z in outline],slot,lift)
    mark("Eye white",[(.049,1.902),(.097,1.918),(.164,1.915),(.125,1.891),(.082,1.891)],9,.005)
    mark("Ink pupil",[(.087,1.913),(.111,1.916),(.116,1.899),(.103,1.891),(.086,1.898)],4,.009)
    mark("Eye line",[(.043,1.90),(.087,1.922),(.166,1.923),(.178,1.927),(.157,1.911),(.098,1.914)],4,.012)
    mark("Heavy expressive eyebrow",[(.047,1.948),(.101,1.963),(.17,1.972),(.159,1.98),(.093,1.98),(.045,1.962)],4,.008)
face_mark("Quiet crooked smile",[(-.059,1.73),(-.012,1.733),(.018,1.730),(.063,1.735),
          (.034,1.725),(-.006,1.723)],3,.006)
hair_rows=[]
for j,(z,rx,ry,cy) in enumerate([(2.01,.249,.215,.003),(2.084,.245,.209,.01),(2.175,.174,.159,.035),(2.208,.088,.095,.043)]):
    row=ellipse(0,cy,z,rx,ry,16)
    hair_rows.append([(x,y,h-(.21*(1+math.sin(i*math.tau/16))/2+.035*math.cos(i*math.tau/16))
                      if j==0 else h) for i,(x,y,h) in enumerate(row)])
shell("Swept hair cap",hair_rows,[4,4,4],4)
for side in (-1, 1):
    # Taper the locks into the cheek instead of ending the hair on a horizontal line.
    lock = [(.10,-.174,2.112),(.209,-.130,2.075),(.249,-.064,1.966),
            (.216,-.087,1.866 if side < 0 else 1.904),(.198,-.133,1.989)]
    front_patch("Asymmetric fringe", [(side*x,y,z) for x,y,z in lock], 4)
front_patch("Asymmetric fringe", [(-.197,-.102,2.091),(-.092,-.198,2.104),
            (.142,-.163,2.106),(.062,-.208,2.049),(-.094,-.191,2.002)], 5)
gem("Topknot",(0,.07,2.24),(.10,.11,.12),4,2)
segment("Hairpin",(-.15,.07,2.25),(.15,.07,2.27),.012,.018,15)

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
    shell("Oar blade", [ellipse(-.62,-.09,z,w,d,8) for z,w,d in [(.14,.03,.023),(.19,.081,.026),(.42,.092,.031),(.55,.064,.027),(.65,.024,.022)]], [8]*4, 8)
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
        shell("Iron shoulder", [ellipse(side*.29,0,z,w,d,10) for z,w,d in [(1.354,.129,.15),(1.38,.14,.165),(1.43,.132,.15),(1.472,.078,.094)]], [8,13,12], 12)
        segment("Forearm plate", (side*.481,-.12,.954), (side*.448,-.074,1.080), .074, .09, 12, 8)
    for j in range(3):
        z = 1.097+j*.075
        w = .185+j*.015
        front_patch("Cross body armor", [(-w,-.139,z+.056),(0,-.206,z+.077),(w,-.139,z+.056),
                    (w*.92,-.148,z+.006),(0,-.207,z-.020),(-w*.92,-.148,z+.006)], 12 if j%2==0 else 13)
    block("Stolen pass seal", (.27, -.24, .83), (.105, .075, .16), 15, .015)
    block("Seal mark", (.27, -.282, .83), (.055, .012, .075), 11)
    segment("Brow scar", (-.18, -.20, 1.98), (-.135, -.213, 1.93), .008, .008, 0, 4)
elif CHARACTER == "Chongshan":
    remove_parts(("Sword", "Topknot", "Hairpin", "Swept hair", "Asymmetric fringe", "Back hair"))
    shell("Cropped hair", [[(x,y,z-.055*(1+math.sin(i*math.tau/16))) for i,(x,y,z) in enumerate(ellipse(0,.008,2.035,.246,.213,16))], ellipse(0,.013,2.10,.23,.20,16), ellipse(0,.026,2.155,.17,.142,16)], [4,4], 4)
    gem("Short angular beard", (0, -.11, 1.63), (.145, .105, .09), 4)
    shell("Heavy shoulder guard", [ellipse(-.31,0,z,w,d,10) for z,w,d in [(1.32,.152,.17),(1.355,.169,.19),(1.441,.158,.17),(1.487,.09,.11)]], [8,12,13], 12)
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
# Publish only a complete frame; Blender on Windows may fail to truncate an existing PNG.
preview = Path(scene.render.filepath)
scene.render.filepath = str(preview.with_suffix(".render.png"))
bpy.ops.render.render(write_still=True)
Path(scene.render.filepath).replace(preview)
print(f"CHARACTER_TRIANGLES={triangles}")
