"""Blender 4.1 background authoring. Metres, +Z up, entrance faces -Y."""
import bpy, math, random, os, json
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Client/Assets/GameMain/Art/WorldTimeline/BlackfangCave')
SRC = os.path.join(ROOT, 'ArtSource/WorldTimeline')
os.makedirs(OUT, exist_ok=True); os.makedirs(SRC, exist_ok=True)
random.seed(41)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
palette = [(0.24,.29,.29,1),(.30,.35,.34,1),(.36,.40,.37,1),(.43,.46,.40,1),
           (.18,.23,.23,1),(.10,.15,.16,1),(.045,.075,.085,1),(.025,.045,.05,1),
           (.26,.36,.22,1),(.34,.43,.26,1),(.43,.49,.30,1),(.50,.48,.37,1),
           (.20,.57,.52,1),(.39,.73,.63,1),(.55,.58,.46,1),(.32,.32,.27,1)]
img=bpy.data.images.new('CavePalette',width=64,height=64)
pixels=[]
for y in range(64):
    for x in range(64): pixels.extend(palette[(y//16)*4+x//16])
img.pixels=pixels; img.filepath_raw=os.path.join(OUT,'CavePalette.png'); img.file_format='PNG'; img.save()
mat=bpy.data.materials.new('CavePalette'); mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.9
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=img; tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
parts=[]
def finish(obj, colors):
    obj.data.materials.clear(); obj.data.materials.append(mat)
    uv=obj.data.uv_layers.new(name='PaletteUV')
    for face in obj.data.polygons:
        color=random.choice(colors); coord=((color%4+.5)/4,(color//4+.5)/4)
        for i in face.loop_indices: uv.data[i].uv=coord
    parts.append(obj); return obj
def mesh(name,verts,faces,colors):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    return finish(obj,colors)
def rock(name,loc,scale,colors=(0,1,2,3),sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=loc)
    obj=bpy.context.object; obj.name=name
    for v in obj.data.vertices:
        v.co*=random.uniform(.86,1.13)
    obj.scale=scale; obj.rotation_euler=(random.uniform(-.15,.15),random.uniform(-.15,.15),random.random()*3)
    return finish(obj,colors)
def slab(name,loc,scale,color):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); obj=bpy.context.object;obj.name=name;obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=obj.modifiers.new('Chipped corners','BEVEL');bevel.width=.10;bevel.segments=1
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=bevel.name)
    return finish(obj,color)
# Nine irregular voussoirs make a real open arch, rather than intersecting spheres.
segments=9
for i in range(segments):
    a=i*math.pi/segments+.018; b=(i+1)*math.pi/segments-.018
    outer=1+random.uniform(-.05,.06)
    ring=[]
    for y in [-.35,2.1+random.uniform(-.15,.25)]:
        for angle,rx,rz in [(a,2.15,2.7),(b,2.15,2.7),(b,4.0*outer,4.9*outer),(a,4.0*outer,4.9*outer)]:
            ring.append((math.cos(angle)*rx,y,.9+math.sin(angle)*rz))
    mesh('Arch_%02d'%i,ring,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],(0,1,2,3))
# Receding interior, dark back wall and floor; inner silhouette remains legible from above.
verts=[]
for y,rx,rz in [(.05,2.15,2.7),(4.7,1.6,2.3)]:
    for i in range(10):
        a=i*math.pi/9;verts.append((math.cos(a)*rx,y,.85+math.sin(a)*rz))
faces=[(i,i+1,i+11,i+10) for i in range(9)]
mesh('Tunnel',verts,faces,(4,5,6))
mesh('Depth',[(0,4.75,.0)]+verts[10:],[(0,i+1,i+2) for i in range(9)],(7,))
slab('Threshold',(0,1.5,.12),(4.3,6,.35),(4,5))
for side in [-1,1]:
    rock('Shoulder',(side*3.65,1.5,1.65),(1.8,2.5,2.4),sub=2)
    rock('Foot',(side*3.4,-.55,.6),(1.3,1.5,.9),sub=1)
    rock('UpperRidge',(side*2.2,1.5,4.45),(1.9,1.8,1.5),sub=1)
    for n in range(3):
        rock('Moss',(side*(2.5+n*.5),.5+n*.7,3.4-n*.65),(.85,.8,.17),(8,9,10),sub=1)
rock('Crown',(-.65,1.55,5.1),(2.1,1.7,.85),sub=1)
rock('CrownMoss',(-.8,1.25,5.78),(1.8,1.25,.12),(8,9,10),sub=1)
for i in range(4):
    slab('ApproachStep',(.12*(-1)**i,-1.0-i*.87,.04-i*.055),(3.5+i*.32,.78,.26),(3,11,14,15))
for side in [-1,1]:
    slab('WeatheredMarker',(side*2.7,-1.35,.85),(.55,.65,1.8),(0,1,2))
    for h in range(3):
        rune=slab('JadeIncision',(side*2.7,-1.686,.65+h*.28),(.18,.025,.055),(12,13))
        rune.rotation_euler[1]=side*.35
    for i in range(4):
        rock('Talus',(side*(3.8+i*.55),-1.1+random.uniform(-.7,.7),.18),(.6,.55,.35),(1,2,3),sub=1)
# Join all decoration into a single mesh with one material / one submesh.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
hero=bpy.context.object;hero.name='BlackfangCave_LOD0'
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
tri=hero.modifiers.new('Triangulate','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
lods=[hero]
for level,ratio in [(1,.48),(2,.20)]:
    obj=hero.copy();obj.data=hero.data.copy();bpy.context.collection.objects.link(obj);obj.name='BlackfangCave_LOD%d'%level
    bpy.context.view_layer.objects.active=obj
    dec=obj.modifiers.new('Silhouette LOD','DECIMATE');dec.ratio=ratio;bpy.ops.object.modifier_apply(modifier=dec.name)
    lods.append(obj)
stats={}
for obj in lods:
    export_name=obj.name
    obj.name=export_name.replace('_LOD','_Detail')
    bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,export_name+'.fbx'),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
    stats[obj.name]={'triangles':sum(len(f.vertices)-2 for f in obj.data.polygons),'materials':len(obj.data.materials)}
    obj.hide_render=obj!=hero;obj.hide_set(obj!=hero)
with open(os.path.join(SRC,'BlackfangCave.stats.json'),'w') as f:json.dump(stats,f,indent=2)
# Studio preview is clearly separate from Unity in-game preview.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.25));floor=bpy.context.object;floor.name='PreviewGround'
ground=bpy.data.materials.new('PreviewGround');ground.diffuse_color=(.19,.24,.20,1);floor.data.materials.append(ground)
world=bpy.context.scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.34,.43,.46,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(Vector((0,0,2))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Warm key',(-6,-7,13),1700,8,(1,.86,.67));area('Cool rim',(5,5,10),2000,7,(.65,.82,1))
data=bpy.data.cameras.new('PreviewCamera');cam=bpy.data.objects.new('PreviewCamera',data);bpy.context.collection.objects.link(cam)
cam.location=(11,-19,14);cam.rotation_euler=(Vector((0,0,2))-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=16
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.filepath=os.path.join(SRC,'BlackfangCave_Blender.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC,'BlackfangCave.blend'))
bpy.ops.render.render(write_still=True)
print('CAVE_STATS '+json.dumps(stats))
