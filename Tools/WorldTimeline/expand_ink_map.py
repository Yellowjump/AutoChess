"""Bake editable ink-map prefab assets without driving the Unity editor.

Region definitions and dimensions are read from WorldTimelineInkMapAuthoring.cs.
Existing prefab GUIDs and root IDs are retained. Image pixels are never modified.
Run with --check for read-only verification after baking.
"""
from pathlib import Path
import argparse
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / "Client/Assets/GameMain/Art/WorldTimeline/InkMap"
AUTHOR = ROOT / "Client/Assets/GameMain/Scripts/Editor/WorldTimeline/WorldTimelineInkMapAuthoring.cs"
NAMESPACE = uuid.UUID("084de7b9-49ee-4e2b-96f0-5d51ad17fe55")
PREFIX = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
outputs = {}


def read(path):
    return path.read_text(encoding="utf-8-sig")


def guid(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return re.search(r"(?m)^guid: (\w+)", read(meta))[1]
    return uuid.uuid5(NAMESPACE, path.relative_to(ROOT).as_posix()).hex


def blocks(text):
    return re.split(r"(?m)(?=^--- !u!)", text)[1:]


def ident(block):
    return int(re.match(r"--- !u!\d+ &(\d+)", block)[1])


def kind(block):
    return int(re.match(r"--- !u!(\d+)", block)[1])


def field(block, key, value):
    result, count = re.subn(r"(?m)^  " + re.escape(key) + r":.*$", "  " + key + ": " + str(value), block)
    assert count == 1, (key, count)
    return result


def vector(x, y, z):
    return "{x: %.7f, y: %.7f, z: %.7f}" % (x, y, z)


def rename(block, name):
    # Use a lambda so regex replacement does not interpret JSON's unicode escapes.
    return re.sub(r"(?m)^  m_Name:.*$", lambda _: "  m_Name: " + json.dumps(name, ensure_ascii=True), block)


def children(block, entries):
    body = "  m_Children:\n" + "".join("  - {fileID: %d}\n" % item for item in entries)
    return re.sub(r"  m_Children:(?: \[\])?\n(?:  - \{fileID: \d+\}\n)*", body, block)


def root_info(entries):
    transform = next(b for b in entries if kind(b) == 4 and "  m_Father: {fileID: 0}" in b)
    go = int(re.search(r"m_GameObject: \{fileID: (\d+)", transform)[1])
    region = next(b for b in entries if kind(b) == 114 and "  localBounds:" in b)
    return ident(transform), go, ident(region)


def meta(path, asset_guid, texture=False):
    meta_path = Path(str(path) + ".meta")
    if meta_path.exists():
        return
    if texture:
        content = re.sub(r"(?m)^guid: \w+", "guid: " + asset_guid,
                         read(FOLDER / "OuterCountryside.png.meta"))
    else:
        content = "fileFormatVersion: 2\nguid: " + asset_guid + "\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    outputs[meta_path] = content


def material(index):
    path = FOLDER / ("Landmark_%d.mat" % index)
    content = rename(read(FOLDER / "Outer_青禾乡野.mat"), "Landmark_%d" % index)
    content = re.sub(r"m_CustomRenderQueue: \d+", "m_CustomRenderQueue: 3010", content)
    atlas_guid = guid(FOLDER / "LandmarkAtlas.png")
    content = re.sub(r"m_Texture: \{fileID: 2800000, guid: \w+, type: 3\}",
                     "m_Texture: {fileID: 2800000, guid: " + atlas_guid + ", type: 3}", content)
    content = content.replace("m_Scale: {x: 1, y: 1}", "m_Scale: {x: 0.5, y: 0.5}")
    content = content.replace("m_Offset: {x: 0, y: 0}", "m_Offset: {x: %.1f, y: %.1f}" %
                              (index % 2 * .5, .5 if index < 2 else 0))
    outputs[path] = content
    asset_guid = guid(path)
    if not Path(str(path) + ".meta").exists():
        outputs[Path(str(path) + ".meta")] = (
            "fileFormatVersion: 2\nguid: " + asset_guid +
            "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return asset_guid


def terrain_material(name, index):
    path = FOLDER / (name + ".mat")
    template = read(FOLDER / "Outer_青禾乡野.mat")
    content = rename(template, name)
    texture = "Shallows.png" if index == 6 else "TerrainAtlas.png"
    content = re.sub(r"(?<=fileID: 2800000, guid: )\w+", guid(FOLDER / texture), content)
    if index != 6:
        content = content.replace("m_Scale: {x: 1, y: 1}", "m_Scale: {x: 0.33333334, y: 0.41}")
        content = content.replace("m_Offset: {x: 0, y: 0}",
                                  "m_Offset: {x: %.8f, y: %.2f}" % (index % 3 / 3, .50 if index < 3 else .10))
    content = content.replace("    - _Paper: 0", "    - _Paper: 0\n    - _TerrainWash: 1")
    outputs[path] = content
    asset_guid = guid(path)
    if not Path(str(path) + ".meta").exists():
        outputs[Path(str(path) + ".meta")] = (
            "fileFormatVersion: 2\nguid: " + asset_guid +
            "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return asset_guid


def remap(block, mapping):
    block = re.sub(r"(?m)^(--- !u!\d+ &)(\d+)", lambda m: m[1] + str(mapping.get(int(m[2]), int(m[2]))), block)
    return re.sub(r"fileID: (\d+)", lambda m: "fileID: " + str(mapping.get(int(m[1]), int(m[1]))), block)


def bake_region(spec, template, origin, spacing, width, depth, landmark_width, landmark_depth, materials):
    name, q, r, mountain, mark, terrain = spec
    entries = blocks(template)
    # Strip previous baked landmarks for deterministic reruns.
    discarded = set()
    for block in entries:
        if kind(block) == 1 and re.search(r'm_Name: "?Landmark_', block):
            discarded.add(ident(block))
            discarded.update(int(x) for x in re.findall(r"component: \{fileID: (\d+)\}", block))
    entries = [b for b in entries if ident(b) not in discarded]
    entries = [re.sub(r"(?m)^  - \{fileID: (\d+)\}\n",
                     lambda m: "" if int(m[1]) in discarded else m[0], b) for b in entries]
    root_transform, root_go, component = root_info(entries)
    component_block = next(b for b in entries if ident(b) == component)
    middle_go = int(re.search(r"middleDetails: \{fileID: (\d+)", component_block)[1])
    far_go = int(re.search(r"farLandmark: \{fileID: (\d+)", component_block)[1])
    transforms = {int(re.search(r"m_GameObject: \{fileID: (\d+)", b)[1]): b for b in entries if kind(b) == 4}
    middle = ident(transforms[middle_go])
    far = ident(transforms[far_go])
    artwork_go = next(b for b in entries if kind(b) == 1 and "  m_Name: InkArtwork\n" in b)
    source_ids = [ident(artwork_go)] + [int(x) for x in re.findall(r"component: \{fileID: (\d+)\}", artwork_go)]
    source = [b for b in entries if ident(b) in source_ids]
    extras = []
    parent_additions = {}
    landmark_parents = (middle, far) if mark >= 0 else ()
    for tier, parent in enumerate(landmark_parents):
        new_ids = [2000000000000000100 + tier * 10 + i for i in range(4)]
        mapping = dict(zip(source_ids, new_ids))
        landmark_transform = next(new for old, new in mapping.items() if kind(next(b for b in source if ident(b) == old)) == 4)
        parent_additions[parent] = landmark_transform
        for source_block in source:
            block = remap(source_block, mapping)
            if kind(block) == 1:
                block = rename(block, "Landmark_主地标" if tier == 0 else "Landmark_远景地标")
            elif kind(block) == 4:
                block = field(block, "m_LocalPosition", vector(-.55 * spacing, .05, .65 * spacing))
                block = field(block, "m_LocalScale", vector(landmark_width * spacing, spacing, landmark_depth * spacing))
                block = field(block, "m_Father", "{fileID: %d}" % parent)
            elif kind(block) == 23:
                block = re.sub(r"(  m_Materials:\n  - \{fileID: 2100000, guid: )\w+", r"\g<1>" + materials[mark], block)
            extras.append(block)
    position = (origin[0] + 1.73205 * (q + r * .5) * spacing,
                origin[1], origin[2] - 1.5 * r * spacing)
    ground_material = terrain_material(name, terrain) if terrain >= 0 else None
    for i, block in enumerate(entries):
        if ident(block) == root_transform:
            block = field(block, "m_LocalPosition", vector(*position))
        elif ident(block) == root_go:
            block = rename(block, name)
        elif ident(block) == component:
            block = field(block, "node", "{x: %d, y: %d}" % (q, r))
            block = re.sub(r"m_Extent: \{[^}]+\}", "m_Extent: " + vector(width * spacing / 2, spacing * .1, depth * spacing / 2), block)
        elif kind(block) == 4 and "  m_LocalScale: {x: 1, y: 1, z: 1}" not in block:
            block = field(block, "m_LocalScale", vector(width * spacing, spacing, depth * spacing))
        elif kind(block) == 23 and ground_material:
            block = re.sub(r"(  m_Materials:\n  - \{fileID: 2100000, guid: )\w+", r"\g<1>" + ground_material, block)
        if ident(block) in parent_additions:
            old_children = [int(x) for x in re.findall(r"(?m)^  - \{fileID: (\d+)\}", block)]
            block = children(block, old_children + [parent_additions[ident(block)]])
        entries[i] = block
    path = FOLDER / "Regions" / (name + ".prefab")
    asset_guid = guid(path)
    outputs[path] = PREFIX + "".join(entries + extras)
    meta(path, asset_guid)
    return asset_guid, root_transform, root_go, component, position


def main(check):
    author = read(AUTHOR)
    terrain_ids = dict((label, int(number)) for label, number in
                       re.findall(r"(Desert|Plain|Grassland|Lake|Falls|Ash|Shallows) = (\d+)", author))
    specs = [(name, int(q), int(r), mountain == "true", int(mark), terrain_ids.get(terrain, -1))
             for name, q, r, mountain, mark, terrain in re.findall(
                 r'new RegionSpec\("([^"]+)",\s*(-?\d+),\s*(-?\d+),\s*(true|false),\s*(-?\d+)(?:,\s*(\w+))?\)', author)]
    assert len(specs) >= 2 and len({s[0] for s in specs}) == len(specs)
    sizes = {name: float(re.search(name + r"\s*=\s*([\d.]+)f", author)[1]) for name in
             ("RegionWidth", "RegionDepth", "LandmarkWidth", "LandmarkDepth")}
    path = FOLDER / "InkMapSample.prefab"
    original = read(path)
    entries = blocks(original)
    origin_match = re.search(r"  Origin: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}", original)
    origin = tuple(float(x) for x in origin_match.groups())
    spacing = float(re.search(r"  NodeSpacing: ([\d.]+)", original)[1])
    outer_path = FOLDER / "Regions/Outer_青禾乡野.prefab"
    inner_path = FOLDER / "Regions/Transition_灵山古隘.prefab"
    templates = {False: read(outer_path), True: read(inner_path)}
    instance_templates = {mountain: next(b for b in entries if kind(b) == 1001 and guid(p) in b)
                          for mountain, p in ((False, outer_path), (True, inner_path))}
    root_transform_block = next(b for b in entries if kind(b) == 4 and "stripped" not in b.splitlines()[0] and "  m_Father: {fileID: 0}" in b)
    root_transform = ident(root_transform_block)
    paper_child = int(re.search(r"  - \{fileID: (\d+)\}", root_transform_block)[1])
    meta(FOLDER / "LandmarkAtlas.png", guid(FOLDER / "LandmarkAtlas.png"), texture=True)
    assert (FOLDER / "TerrainAtlas.png").exists()
    meta(FOLDER / "TerrainAtlas.png", guid(FOLDER / "TerrainAtlas.png"), texture=True)
    assert (FOLDER / "Shallows.png").exists()
    meta(FOLDER / "Shallows.png", guid(FOLDER / "Shallows.png"), texture=True)
    materials = [material(i) for i in range(4)]
    new_instances, child_ids, component_ids = [], [paper_child], []
    for index, spec in enumerate(specs):
        asset_guid, transform, go, component, position = bake_region(spec, templates[spec[3]], origin, spacing,
            sizes["RegionWidth"], sizes["RegionDepth"], sizes["LandmarkWidth"], sizes["LandmarkDepth"], materials)
        instance_id = 3000000000000000000 + index * 100
        transform_id, component_id = instance_id + 1, instance_id + 2
        child_ids.append(transform_id); component_ids.append(component_id)
        template = instance_templates[spec[3]]
        template_guid = guid(inner_path if spec[3] else outer_path)
        instance = template.replace(template_guid, asset_guid)
        instance = re.sub(r"(?m)^(--- !u!1001 &)\d+", r"\g<1>" + str(instance_id), instance)
        for axis, value in zip("xyz", position):
            instance = re.sub(r"(propertyPath: m_LocalPosition\." + axis + r"\n      value: )[^\n]+", r"\g<1>%.7f" % value, instance)
        instance = re.sub(r"(propertyPath: m_Name\n      value: )[^\n]+", lambda m: m[1] + json.dumps(spec[0], ensure_ascii=True), instance)
        new_instances.append(instance)
        new_instances.append("--- !u!4 &%d stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: %d, guid: %s, type: 3}\n  m_PrefabInstance: {fileID: %d}\n  m_PrefabAsset: {fileID: 0}\n" % (transform_id, transform, asset_guid, instance_id))
        region_guid = guid(ROOT / "Client/Assets/GameMain/Scripts/WorldTimelineDebug/WorldTimelineRegion.cs")
        new_instances.append("--- !u!114 &%d stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {fileID: %d, guid: %s, type: 3}\n  m_PrefabInstance: {fileID: %d}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n" % (component_id, component, asset_guid, instance_id, region_guid))
    preserved = []
    for block in entries:
        if kind(block) == 1001 or "stripped" in block.splitlines()[0]:
            continue
        if ident(block) == root_transform:
            block = children(block, child_ids)
        if "  regions:\n" in block:
            block = re.sub(r"  regions:\n(?:  - \{fileID: \d+\}\n)+", "  regions:\n" + "".join("  - {fileID: %d}\n" % n for n in component_ids), block)
        if "  CameraFieldOfView:" in block:
            if "  FocusZoom:" not in block:
                block += "  FocusZoom: 0.31\n"
            else:
                block = field(block, "FocusZoom", .31)
        preserved.append(block)
    outputs[path] = PREFIX + "".join(preserved + new_instances)
    for target, content in outputs.items():
        if target.suffix == ".prefab":
            ids = [ident(b) for b in blocks(content)]
            assert len(ids) == len(set(ids)), "Duplicate prefab IDs: " + str(target)
            local_refs = {int(n) for n in re.findall(r"\{fileID: (\d+)\}", content)} - {0}
            assert local_refs.issubset(set(ids)), "Dangling local prefab reference: " + str(target)
            assert "SRPDefaultUnlit" not in content  # Shader belongs to the material, not baked UI.
    births = []
    for row in read(ROOT / "Client/Assets/GameMain/Data/DataTables/StartingCultivators.txt").splitlines():
        fields = row.split("\t")
        if len(fields) < 6 or not fields[1].isdigit():
            continue
        q, r = int(fields[3]), int(fields[4])
        covered = any(abs(1.73205 * (q - s[1] + (r - s[2]) * .5)) < sizes["RegionWidth"] * .35
                      and abs(1.5 * (r - s[2])) < sizes["RegionDepth"] * .35 for s in specs if not s[3])
        assert covered, "Birthplace lacks scenery: " + fields[2]
        births.append(fields[2])
    assert births
    for target, content in outputs.items():
        if check:
            assert target.exists() and read(target) == content, "Asset differs from bake: " + str(target)
        elif not target.exists() or read(target) != content:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8", newline="\n")
    print(json.dumps({"mode": "check" if check else "bake", "regions": len(specs),
                      "birthplacesCovered": len(births), "maxActiveSceneryRenderers": 1 + 2 * len(specs),
                      "landmarkAtlas": "LandmarkAtlas.png", "filesPrepared": len(outputs)}, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    main(parser.parse_args().check)
