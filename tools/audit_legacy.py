"""Read-only inventory of tracked Unity legacy assets; run from any directory."""
import collections
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
PROJECT = "New Unity Project/"
ASSETS = PROJECT + "Assets/"
files = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode().split("\0")
files = [f for f in files if f]
tracked = set(files)
asset_files = [f for f in files if f.startswith(ASSETS)]
metas = [f for f in asset_files if f.endswith(".meta")]
guid_paths = collections.defaultdict(list)
for name in metas:
    match = re.search(r"^guid: ([0-9a-f]{32})$", (ROOT / name).read_text(errors="replace"), re.M)
    if match:
        guid_paths[match[1]].append(name[:-5])

serialized = [f for f in files if f.startswith(PROJECT) and pathlib.Path(f).suffix in
              {".unity", ".prefab", ".asset", ".mat", ".controller", ".anim"}]
missing = collections.defaultdict(set)
references = {}
binary = []
text_count = 0
for name in serialized:
    data = (ROOT / name).read_bytes()
    if not data.startswith(b"%YAML"):
        binary.append(name)
        continue
    text_count += 1
    references[name] = set()
    for guid in re.findall(rb"guid: ([0-9a-f]{32})", data):
        guid = guid.decode()
        references[name].add(guid)
        # Unity built-in references have zero-filled prefixes, e.g. 000...f000... .
        if guid not in guid_paths and not guid.startswith("0000000000000000"):
            missing[guid].add(name)

settings = (ROOT / PROJECT / "ProjectSettings/EditorBuildSettings.asset").read_text()
build_scenes = [{"enabled": enabled == "1", "path": path,
                 "present": PROJECT + path in tracked}
                for enabled, path in re.findall(r"enabled: ([01])\s+path: (.+)", settings)]
reachable = set()
pending = [PROJECT + s["path"] for s in build_scenes if s["enabled"]]
reachable_missing = collections.defaultdict(set)
while pending:
    name = pending.pop()
    if name in reachable:
        continue
    reachable.add(name)
    for guid in references.get(name, ()):
        if guid in guid_paths:
            pending.extend(guid_paths[guid])
        elif not guid.startswith("0000000000000000"):
            reachable_missing[guid].add(name)
result = {
    "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
    "tracked_file_count": len(files),
    "assets_file_count_including_meta": len(asset_files),
    "asset_extensions_excluding_meta": dict(collections.Counter(pathlib.Path(f).suffix.lower()
        for f in asset_files if not f.endswith(".meta")).most_common()),
    "missing_file_meta": [f for f in asset_files if not f.endswith(".meta") and f + ".meta" not in tracked],
    "orphan_meta": [f for f in metas if f[:-5] not in tracked and not (ROOT / f[:-5]).is_dir()],
    "duplicate_guids": {g: p for g, p in guid_paths.items() if len(p) > 1},
    "yaml_serialized_files_scanned": text_count,
    "binary_or_non_yaml_serialized_files_not_scanned": binary,
    "unresolved_guid_references": {g: sorted(p) for g, p in sorted(missing.items())},
    "build_scenes": build_scenes,
    "enabled_scene_static_dependency_count": len(reachable),
    "enabled_scene_static_unresolved_guids": {g: sorted(p) for g, p in sorted(reachable_missing.items())},
    "enabled_scene_binary_dependencies_not_scanned": sorted(set(binary) & reachable),
    "font_files": [f for f in asset_files if pathlib.Path(f).suffix.lower() in {".ttf", ".otf", ".ttc"}],
    "certificate_files_review_required": [f for f in files if pathlib.Path(f).suffix.lower() in {".pfx", ".pem", ".key"}],
    "lfs_pointer_files": [f for f in files if (ROOT / f).stat().st_size < 1024 and
        (ROOT / f).read_bytes().startswith(b"version https://git-lfs.github.com/spec/v1")],
    "limitations": ["Static tracked-file/GUID inventory only; not a Unity import or build test.",
        "Binary serialized assets and fileID references require Unity inspection.",
        "Scene dependency traversal excludes runtime Resources/Photon lookups, importer metadata, and embedded unitypackages.",
        "Presence of binaries does not establish Android ARM64 compatibility or redistribution rights."]
}
print(json.dumps(result, ensure_ascii=False, indent=2))
