import bpy
from pathlib import Path

root = Path.cwd()
asset = bpy.data.collections["Linxi Ferry"]
# Display-space colors chosen against the map's warm paper.
palette = {
    "Weathered limestone": (.69, .65, .57),
    "Pale cut stone": (.73, .69, .61),
    "River shale": (.51, .49, .43),
    "Aged dark timber": (.40, .35, .29),
    "Charcoal celadon tiles": (.34, .37, .34),
    "Glazed tile edges": (.40, .42, .37),
    "Silt and moss": (.49, .49, .39),
    "Willow leaves 0": (.44, .46, .35),
    "Willow leaves 1": (.49, .51, .38),
    "Willow leaves 2": (.55, .55, .43),
}
vertices, faces, face_colors = [], [], []
for part in asset.objects:
    if part.name in ("River", "River reflections"):
        continue
    mesh = part.data
    start = len(vertices)
    vertices.extend(part.matrix_world @ v.co for v in mesh.vertices)
    for polygon in mesh.polygons:
        mat = mesh.materials[polygon.material_index]
        rgb = palette[mat.name]
        if part.name in ("Bridge masonry", "Arch voussoirs", "Stone paving", "Bridge balustrades"):
            rgb = tuple(channel * .73 for channel in rgb)
        color = (*rgb, 1.0)
        faces.append(tuple(start + index for index in polygon.vertices))
        face_colors.append(color)

mesh = bpy.data.meshes.new("Linxi Ferry colored mesh")
mesh.from_pydata(vertices, [], faces)
mesh.update()
colors = mesh.color_attributes.new(name="Albedo", type="BYTE_COLOR", domain="CORNER")
for polygon,color in zip(mesh.polygons,face_colors):
    for loop in polygon.loop_indices:
        colors.data[loop].color_srgb = color
obj = bpy.data.objects.new("LinxiFerry",mesh)
bpy.context.scene.collection.objects.link(obj)
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(
    filepath=str(root / "Client/Assets/GameMain/Art/WorldTimeline/InkMap/Resources/WorldTimeline/LinxiFerry.fbx"),
    use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",
    bake_space_transform=True,colors_type="SRGB",bake_anim=False)
print("Exported", len(vertices), "vertices and", len(faces), "polygons")
