"""Shared xuan-paper ink washes for the animated opening shots."""

import bpy


PAPER = (.69, .615, .495)
FOG = (.625, .588, .501)


def wash_material(material, kind="ink", mist=1.0):
    """Build matte, camera-lit pigments with object-local pigment granulation."""
    base = material.diffuse_color[:3]
    gray = base[0] * .23 + base[1] * .65 + base[2] * .12
    color = tuple(gray * .53 + channel * .47 for channel in base)
    material.use_nodes = True
    material.node_tree.nodes.clear()
    nodes = material.node_tree.nodes
    links = material.node_tree.links

    def node(type_name, x, y, label=""):
        value = nodes.new(type_name)
        value.location = (x, y)
        value.label = label
        return value

    def attach(target, value):
        if isinstance(value, bpy.types.NodeSocket):
            links.new(value, target)
        else:
            target.default_value = value

    def math(operation, a, b, x, y):
        value = node('ShaderNodeMath', x, y)
        value.operation = operation
        attach(value.inputs[0], a)
        attach(value.inputs[1], b)
        return value.outputs[0]

    def mix(factor, a, b, x, y, label=""):
        value = node('ShaderNodeMixRGB', x, y, label)
        attach(value.inputs[0], factor)
        attach(value.inputs[1], a)
        attach(value.inputs[2], b)
        return value.outputs[0]

    def map_range(source, low, high, dark, light, x, y):
        value = node('ShaderNodeMapRange', x, y)
        value.interpolation_type = 'SMOOTHERSTEP'
        for socket, input_value in zip(value.inputs, [source, low, high, dark, light]):
            attach(socket, input_value)
        return value.outputs[0]

    coordinate = node('ShaderNodeTexCoord', -1080, 220)
    direction = node('ShaderNodeVectorMath', -880, 220, "Brush direction")
    direction.operation = 'MULTIPLY'
    brush_scale = (1.3, 6.5, 3.2) if kind == 'river' else (1.5, 2.7, 4.0)
    attach(direction.inputs[0], coordinate.outputs['Object'])
    attach(direction.inputs[1], brush_scale)
    broad = node('ShaderNodeTexNoise', -670, 235, "Ink pooling")
    attach(broad.inputs['Vector'], direction.outputs[0])
    broad.inputs['Scale'].default_value = .64 if kind in ('ridge', 'river', 'sky') else 4.2
    broad.inputs['Detail'].default_value = 3
    broad.inputs['Roughness'].default_value = .68

    fine = node('ShaderNodeTexNoise', -670, -30, "Pigment granulation")
    attach(fine.inputs['Vector'], direction.outputs[0])
    fine.inputs['Scale'].default_value = 14 if kind in ('ridge', 'river', 'sky') else 51
    fine.inputs['Detail'].default_value = 2
    broad_pigment = math('MULTIPLY', broad.outputs['Fac'], .77, -450, 215)
    fine_pigment = math('MULTIPLY', fine.outputs['Fac'], .23, -450, -20)
    pigment = math('ADD', broad_pigment, fine_pigment, -275, 215)
    tone = map_range(pigment, .24, .76, .06, .94, -80, 235)

    geom = node('ShaderNodeNewGeometry', -665, -400)
    normal = node('ShaderNodeVectorMath', -450, -270, "Soft upper-left wash")
    normal.operation = 'DOT_PRODUCT'
    attach(normal.inputs[0], geom.outputs['Normal'])
    attach(normal.inputs[1], (-.53, -.18, .79))
    lit = map_range(normal.outputs['Value'], -.7, 1.0, .04, .84, -275, -230)

    if kind == 'river':
        dark, light = (.43, .431, .388), (.66, .607, .50)
        near = mix(tone, (*dark, 1), (*light, 1), 160, 245, "Water on silk")
    elif kind == 'sky':
        near = mix(tone, (*FOG, 1), (*PAPER, 1), 160, 245, "Sky left as paper")
    elif kind == 'lantern':
        near = mix(tone, (.38, .13, .039, 1), (.93, .62, .20, 1), 160, 245, "Warm lamp stain")
    else:
        dark = tuple(max(.009, value * .66) for value in color)
        light = tuple(min(.88, value * 1.05 + .033) for value in color)
        if kind == 'ridge':
            dark = tuple(channel * .7 for channel in light)
            near = mix(tone, (*dark, 1), (*light, 1), 160, 245, "Mountain brush marks")
            separate = node('ShaderNodeSeparateXYZ', -650, -685)
            attach(separate.inputs[0], coordinate.outputs['Generated'])
            washout = map_range(separate.outputs['Z'], .0, .68, .65, 0, 385, -480)
            near = mix(washout, near, (*FOG, 1), 460, 250, "Clouds around the mountain foot")
        else:
            ink = math('MULTIPLY', tone, .38, 125, -40)
            ink = math('ADD', ink, math('MULTIPLY', lit, .64, -55, -165), 310, -42)
            near = mix(ink, (*dark, 1), (*light, 1), 485, 240, "Matte ink")
            rim = node('ShaderNodeLayerWeight', 120, -310, "Dry pigment at the edge")
            rim.inputs['Blend'].default_value = .3
            edge = map_range(rim.outputs['Facing'], .55, .99, 0, .44, 325, -260)
            near = mix(edge, near, (*(channel * .53 for channel in dark), 1), 670, 240)

    if kind not in ('lantern', 'sky'):
        view = node('ShaderNodeCameraData', 500, -320)
        low, high = (9, 50) if kind in ('river', 'ridge') else (6, 30)
        distance = map_range(view.outputs['View Distance'], low, high, .025, .86 * mist, 685, -110)
        fog_color = mix(tone, (*FOG, 1), (*PAPER, 1), 880, -200)
        near = mix(distance, near, fog_color, 1090, 220, "River air")
    out = node('ShaderNodeOutputMaterial', 1500, 220)
    emission = node('ShaderNodeEmission', 1300, 220)
    attach(emission.inputs['Color'], near)
    links.new(emission.outputs[0], out.inputs['Surface'])


def apply_ink_washes():
    for material in bpy.data.materials:
        name = material.name
        if name == 'River':
            wash_material(material, 'river')
        elif name.startswith(('Mist ridge', 'Pass | Distant mist')):
            wash_material(material, 'ridge')
        elif name in ('Pass | Dawn sky', 'Ink-paper sky'):
            wash_material(material, 'sky')
        elif 'lantern paper' in name:
            wash_material(material, 'lantern')
        else:
            wash_material(material, mist=.72)


def make_paper_composite(scene):
    """Layer subtle fiber mottling over the render without changing shot timing."""
    scene.use_nodes = True
    tree = scene.node_tree
    tree.nodes.clear()
    layers = tree.nodes.new('CompositorNodeRLayers')
    layers.location = (-400, 100)
    texture = bpy.data.textures.new("Xuan paper fibers", type='CLOUDS')
    texture.noise_scale = .009
    texture.noise_depth = 2
    fibers = tree.nodes.new('CompositorNodeTexture')
    fibers.location = (-405, -130)
    fibers.texture = texture
    fibers.inputs['Scale'].default_value = (1, 1.75, 1)
    paper = tree.nodes.new('CompositorNodeMixRGB')
    paper.label = "Fine xuan paper tooth"
    paper.location = (-150, 100)
    paper.blend_type = 'MULTIPLY'
    paper.inputs[0].default_value = .095
    tree.links.new(layers.outputs['Image'], paper.inputs[1])
    tree.links.new(fibers.outputs['Color'], paper.inputs[2])
    output = tree.nodes.new('CompositorNodeComposite')
    output.location = (95, 100)
    tree.links.new(paper.outputs[0], output.inputs['Image'])
