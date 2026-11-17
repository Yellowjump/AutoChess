"""Build the Linxi Ferry landmark, export its mesh, and render a studio preview."""
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

OUT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
RNG = random.Random(311)
bpy.ops.wm.read_factory_settings(use_empty=True)
asset = bpy.data.collections.new("Linxi Ferry")
bpy.context.scene.collection.children.link(asset)
parts = {}


def material(name, dark, light, scale=11, roughness=.87):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*light, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    surface = nodes.get("Principled BSDF")
    surface.inputs["Roughness"].default_value = roughness
    coords = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 3
    links.new(coords.outputs["Object"], noise.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = .18
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[1].position = .79
    ramp.color_ramp.elements[1].color = (*light, 1)
    links.new(noise.outputs["Fac"], ramp.inputs[0])
    links.new(ramp.outputs["Color"], surface.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .20
    bump.inputs["Distance"].default_value = .032
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], surface.inputs["Normal"])
    return mat


stone = material("Weathered limestone", (.25,.22,.17), (.61,.55,.43))
pale = material("Pale cut stone", (.34,.30,.24), (.72,.66,.53))
old = material("River shale", (.13,.13,.10), (.39,.35,.28))
wood = material("Aged dark timber", (.072,.042,.024), (.27,.174,.101))
roof = material("Charcoal celadon tiles", (.043,.067,.062), (.16,.215,.174), 13)
rim = material("Glazed tile edges", (.075,.097,.079), (.29,.32,.23))
water = material("Jade river", (.049,.102,.09), (.20,.30,.241), 5, .33)
water_edge = material("Shallow river", (.12,.18,.145), (.31,.38,.279), 8, .48)
ripple = material("Reflected current", (.22,.28,.23), (.51,.55,.41), 9, .49)
earth = material("Silt and moss", (.15,.15,.084), (.36,.35,.20), 7)
leaves = [
    material("Willow leaves " + str(i), tuple(v * .53 for v in color), color)
    for i, color in enumerate(((.24,.29,.105), (.34,.36,.15), (.45,.44,.20)))
]


def mesh(group, vertices, faces, mat):
    if group not in parts:
        parts[group] = ([], [], [], [])
    points, polygons, materials, indices = parts[group]
    if mat not in materials:
        materials.append(mat)
    start = len(points)
    points.extend(vertices)
    polygons.extend(tuple(start + i for i in face) for face in faces)
    indices.extend([materials.index(mat)] * len(faces))


def solid(group, ring_a, ring_b, mat):
    count = len(ring_a)
    sides = [(i, (i+1)%count, (i+1)%count+count, i+count) for i in range(count)]
    mesh(group, ring_a + ring_b, [tuple(reversed(range(count))), tuple(range(count, count*2)), *sides], mat)


def slab(group, x, y, z, sx, sy, sz, mat, chip=.13):
    polygon = [(-.5+chip,-.5),(.5-chip,-.5),(.5,-.5+chip),(.5,.5-chip),
               (.5-chip,.5),(-.5+chip,.5),(-.5,.5-chip),(-.5,-.5+chip)]
    solid(group, [(x+u*sx,y+v*sy,z-sz/2) for u,v in polygon],
          [(x+u*sx,y+v*sy,z+sz/2) for u,v in polygon], mat)


def tube(group, start, end, r0, r1, mat, sides=6):
    start, end = Vector(start), Vector(end)
    axis = (end-start).normalized()
    u = axis.cross(Vector((0,1,0))).normalized()
    if u.length < .1:
        u = axis.cross(Vector((1,0,0))).normalized()
    v = axis.cross(u)
    ring = [u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides) for i in range(sides)]
    solid(group, [start+d*r0 for d in ring], [end+d*r1 for d in ring], mat)


def rock(group, x, y, z, rx, ry, rz, mat):
    angle = RNG.uniform(0,math.tau)
    boundary = []
    for i in range(7):
        a = angle + i*math.tau/7
        factor = RNG.uniform(.72,1.18)
        boundary.append((math.cos(a)*rx*factor,math.sin(a)*ry*factor))
    solid(group, [(x+u,y+v,z) for u,v in boundary],
          [(x+u*.70+rx*.14,y+v*.60,z+rz*RNG.uniform(.62,1.13)) for u,v in boundary], mat)


# Jagged shorelines and lower inboard water, with all stone islands resting at the origin plane.
river_rows = []
for i in range(14):
    y = -2.72 + i*.40
    center = .36*math.sin(y*.9) - y*.18
    width = (1.03 + .20*math.cos(y*1.5)) * min(1, .53 + .30*min(i,13-i))
    river_rows.append([(center+f*width,y,.045 + RNG.uniform(-.006,.006)) for f in (-1,-.81,-.28,.32,.83,1)])
for i in range(13):
    for j in range(5):
        mesh("River", [river_rows[i][j],river_rows[i][j+1],river_rows[i+1][j+1],river_rows[i+1][j]],
             [(0,1,2,3)], water_edge if j in (0,4) else water)
for i in range(34):
    y = RNG.uniform(-2.32,2.33)
    x = .36*math.sin(y*.9)-y*.18+RNG.uniform(-.70,.70)
    length = RNG.uniform(.09,.38)
    mesh("River reflections", [(x-length,y-.055,.061),(x-.03,y+.018,.061),(x+length,y+.039,.061),
                               (x+.055,y+.033,.061),(x-.04,y+.010,.061)], [(0,1,2,3,4)], ripple)
for side in (-1,1):
    for i in range(17):
        y = -2.36+i*.27+RNG.uniform(-.12,.12)
        edge = .36*math.sin(y*.9)-y*.18
        x = edge+side*RNG.uniform(1.11,1.44)
        rock("River banks",x,y,0,RNG.uniform(.27,.60),RNG.uniform(.22,.46),
             RNG.uniform(.08,.25),RNG.choice((old,stone,earth)))
    for i in range(10):
        rock("River banks",side*RNG.uniform(1.45,2.48),RNG.uniform(-.86,1.48),0,
             RNG.uniform(.24,.51),RNG.uniform(.23,.44),RNG.uniform(.09,.24),RNG.choice((old,stone,earth)))


def deck(x):
    return .25 + .91 * (max(0,1-(x/2.23)**2))**.76


def intrados(x):
    return .055 + .82 * math.sqrt(max(0,1-(x/1.26)**2))


for sign in (-1,1):
    for i in range(5):
        x = sign * (2.12+i*.146)
        z = .31-i*.049
        slab("Bridge masonry",x,0,z*.5,.17,1.22-i*.045,z,pale if i%2 else stone)
    # Foundation at the ends leaves the bridge opening unobstructed.
    rock("River banks",sign*2.32,-.14,0,.53,.82,.21,stone)

# Segmented arch ring with a fully modeled intrados and ashlar spandrels.
for i in range(18):
    a = i*math.pi/18+.007
    b = (i+1)*math.pi/18-.007
    cut = []
    for y in (-.62,.62):
        cut.append([(math.cos(a)*1.26,y,.055+math.sin(a)*.82),
                    (math.cos(b)*1.26,y,.055+math.sin(b)*.82),
                    (math.cos(b)*1.46,y,.055+math.sin(b)*1.00),
                    (math.cos(a)*1.46,y,.055+math.sin(a)*1.00)])
    solid("Arch voussoirs", *cut, RNG.choice((pale,pale,stone)))
for i in range(20):
    a = -2.14+i*.214+.006
    b = a+.200
    low_a, low_b = max(.06,intrados(a)-.024), max(.06,intrados(b)-.024)
    section = [[(a,y,low_a),(b,y,low_b),(b,y,deck(b)-.07),(a,y,deck(a)-.07)] for y in (-.58,.58)]
    solid("Bridge masonry", *section, RNG.choice((stone,stone,pale)))
    for j in range(3):
        y = -.56+j*.378
        mesh("Stone paving",[(a,y,deck(a)),(b,y,deck(b)),(b,y+.367,deck(b)),(a,y+.367,deck(a))],
             [(0,1,2,3)],RNG.choice((pale,pale,stone)))
    for side in (-1,1):
        y = .602*side
        tube("Bridge balustrades",(a,y,deck(a)+.19),(b+.015,y,deck(b+.015)+.19),.026,.026,pale,4)
        tube("Bridge balustrades",(a,y,deck(a)+.055),(b+.015,y,deck(b+.015)+.055),.031,.031,stone,4)
        if i%4 == 0 or i == 19:
            z = deck(a)+.132
            slab("Bridge balustrades",a,y,z,.12,.126,.29,pale)
            tube("Bridge balustrades",(a,y,z+.15),(a,y,z+.23),.070,.043,stone,7)


# An open timber pavilion, offset on the opposite approach.
px, py = 2.05, 1.05
slab("Pavilion foundation",px,py,.138,1.52,1.38,.25,old)
for i in range(8):
    slab("Pavilion timber",px,py-.61+i*.17,.278,1.37,.161,.061,wood,.035)
for x in (px-.54,px+.54):
    for y in (py-.48,py+.48):
        slab("Pavilion timber",x,y,.79,.092,.098,1.0,wood)
        slab("Pavilion foundation",x,y,.32,.168,.171,.093,pale)
        for dx in (-.15,.15):
            tube("Pavilion timber",(x,y,1.14),(x+dx,y,1.32),.027,.033,wood,4)
for y in (py-.54,py+.54):
    slab("Pavilion timber",px,y,1.27,1.29,.084,.12,wood)
for x in (px-.56,px+.56):
    slab("Pavilion timber",x,py,1.27,.082,1.18,.12,wood)
    slab("Pavilion timber",x,py,.59,.049,1.04,.056,wood)


def roof_ring(t, angle):
    # A hip roof with a stronger sweep near the corners.
    sx, sy = 1.01*(1-t)+.50*t, .91*(1-t)+.080*t
    ca, sa = math.cos(angle), math.sin(angle)
    x = math.copysign(abs(ca)**.54,ca)*sx
    y = math.copysign(abs(sa)**.54,sa)*sy
    z = 1.32 + .65*t*t + (1-t)**3*(.07+.16*abs(math.sin(2*angle))**3)
    return (px+x,py+y,z)


angles = 32
for i in range(angles):
    a,b = i*math.tau/angles,(i+1)*math.tau/angles
    for d in range(8):
        t0,t1 = d/8,(d+1)/8
        segment = [roof_ring(t0,a),roof_ring(t0,b),roof_ring(t1,b),roof_ring(t1,a)]
        mesh("Swept ceramic roof", segment, [(0,1,2,3)], rim if d == 0 and i%4 == 0 else roof)
        lift = Vector((0,0,.020))
        tube("Swept ceramic roof",Vector(segment[0])+lift,Vector(segment[3])+lift,.020,.017,roof,5)
    tube("Roof trim",roof_ring(0,a),roof_ring(0,b),.035,.035,rim,5)
mesh("Swept ceramic roof", [roof_ring(1,i*math.tau/angles) for i in range(angles)],
     [tuple(range(angles))], roof)
tube("Roof trim",(px-.55,py,1.97),(px+.55,py,1.97),.044,.044,rim,6)
for side in (-1,1):
    tube("Roof trim",(px+side*.50,py,1.97),(px+side*.64,py,2.10),.046,.018,rim,6)
for i in range(4):
    slab("Pavilion timber",px-.20,py-.65-i*.13,.19-i*.023, .75,.124,.069,wood,.06)
for x in (px-.59,px+.21):
    tube("Pavilion timber",(x,py-1.05,.0),(x,py-1.05,.42),.055,.052,wood,7)


# An asymmetrical willow with individually modeled tapered branches and sparse leaf clusters.
trunk = [(-2.16,.85,.13),(-2.29,.90,.63),(-2.63,.95,1.08),(-2.51,1.02,1.56),(-2.12,1.11,1.99)]
for i,(a,b) in enumerate(zip(trunk,trunk[1:])):
    tube("Willow branches",a,b,.20-i*.039,.164-i*.034,wood,8)
for i in range(6):
    angle=i*math.tau/6
    tube("Willow branches",trunk[0],(-2.16+.46*math.cos(angle),.85+.42*math.sin(angle),.12),.112,.028,wood)
for i in range(11):
    angle=i*math.tau/11 + RNG.uniform(-.23,.23)
    start=Vector(trunk[2+i%3])
    spread=Vector((math.cos(angle),math.sin(angle)*.75,0))
    shoulder=start+spread*.53+Vector((0,0,RNG.uniform(.46,.79)))
    bud=shoulder+spread*RNG.uniform(.43,.79)+Vector((0,0,RNG.uniform(.02,.25)))
    tube("Willow branches",start,shoulder,.057,.031,wood)
    tube("Willow branches",shoulder,bud,.031,.009,wood,5)
    for j in range(5):
        tip=shoulder.lerp(bud,.31+j*.15)+Vector((RNG.uniform(-.25,.25),RNG.uniform(-.28,.28),.07))
        previous=tip
        fall=RNG.uniform(.35,.99)
        for k in range(7):
            point=tip+spread*(.12*k/6)+Vector((0,0,-fall*k/6))
            if k%2 == 1:
                tube("Willow twigs",previous,point,.008,.005,wood,4)
                previous=point
            for sign in (-1,1):
                direction=Vector((.09*sign,RNG.uniform(-.09,.09),-.18))*RNG.uniform(.78,1.30)
                offset=Vector((.026,.012,0))
                mesh("Willow foliage",[point,point+direction*.49-offset,point+direction,
                     point+direction*.49+offset],[(0,1,2,3)],RNG.choice(leaves))


for i in range(18):
    y=RNG.uniform(-2.10,2.00)
    x=RNG.choice((-1,1))*RNG.uniform(1.15,1.74)-y*.14
    for j in range(5):
        base=Vector((x+RNG.uniform(-.15,.15),y+RNG.uniform(-.12,.12),.13))
        tip=base+Vector((RNG.uniform(-.12,.13),RNG.uniform(-.10,.10),RNG.uniform(.21,.45)))
        mesh("River reeds",[base,base+Vector((.025,.020,0)),tip],[(0,1,2)],RNG.choice(leaves))


models=[]
for name,(vertices,faces,materials,indices) in parts.items():
    geometry=bpy.data.meshes.new(name)
    geometry.from_pydata(vertices,[],faces)
    geometry.validate()
    geometry.update()
    for mat in materials:
        geometry.materials.append(mat)
    for polygon,index in zip(geometry.polygons,indices):
        polygon.material_index=index
    obj=bpy.data.objects.new(name,geometry)
    asset.objects.link(obj)
    if name=="River reflections":
        obj.visible_shadow=False
    models.append(obj)
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)

for obj in models:
    obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/"LinxiFerry.fbx"),use_selection=True,
                         object_types={"MESH"},axis_forward="-Z",axis_up="Y",bake_anim=False)
bpy.ops.object.select_all(action="DESELECT")

# Camera and render stage for inspection. These are excluded from the FBX.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.026))
bpy.context.object.name="Preview paper"
bpy.context.object.data.materials.append(material("Warm rice paper",(.52,.42,.29),(.82,.73,.54),45))
scene=bpy.context.scene
scene.world=bpy.data.worlds.new("Soft ambient")
scene.world.use_nodes=True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value=(.71,.78,.82,1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value=.37


def light(name, loc, energy, color, size):
    data=bpy.data.lights.new(name,"AREA")
    data.energy=energy
    data.color=color
    data.shape="DISK"
    data.size=size
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=loc
    obj.rotation_euler=(-obj.location).to_track_quat("-Z","Y").to_euler()


light("Warm soft key",(-4,-5,7),1050,(1.0,.91,.76),5)
light("Neutral fill",(4,4,6),650,(.82,.90,1),6)
optic=bpy.data.cameras.new("Map angle")
camera=bpy.data.objects.new("Map angle",optic)
scene.collection.objects.link(camera)
camera.location=(4.5,-9,8.0)
camera.rotation_euler=(Vector((-.27,.10,1.06))-camera.location).to_track_quat("-Z","Y").to_euler()
optic.type="ORTHO"
optic.ortho_scale=9.45
scene.camera=camera
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1280
scene.render.resolution_y=860
scene.render.resolution_percentage=100
scene.view_settings.view_transform="AgX"
scene.render.filepath="//LinxiFerry-preview.png"
bpy.context.preferences.filepaths.save_version=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_perspective="CAMERA"
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"LinxiFerry.blend"))
bpy.ops.render.render(write_still=True)
print("Landmark triangles:",sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in models))
