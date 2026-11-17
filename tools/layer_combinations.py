"""What a mod's own files do only when ANOTHER mod is installed, sorted by whether it can matter.

Used by layer-build.py. Two readings of the same files:
  - Content Patcher patches whose `When` names HasMod, followed through Include files. Each is
    sorted by its Target: data the lexicon reads (objects, shops, locations, maps, ...) or not
    (dialogue, schedules, portraits, events).
  - Every line that says HasMod anywhere else (a token inside a value, another framework's file).
    These cannot be sorted by machine and are listed for a person.
"""
from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from pathlib import Path

import json5

# Asset names the lexicon's inputs are built from. A patch to anything else changes no answer.
READ_TARGETS = (
    "data/objects", "data/bigcraftables", "data/shops", "data/locations", "data/fish", "data/crops",
    "data/fruittrees", "data/machines", "data/cookingrecipes", "data/craftingrecipes", "data/farmanimals",
    "data/buildings", "data/garbagecans", "data/fishponddata", "data/passivefestivals", "data/monsters",
    "data/wildtrees", "data/festivals/festivaldates", "data/monsterslayerquests", "data/bundles",
    "data/minecarts", "data/triggeractions", "data/weapons", "data/tools", "data/locationcontexts", "maps/",
)
MOD_ID_PATTERN = re.compile(r"[A-Za-z][\w\-]*(?:\.[\w\-]+)+")
NOT_MOD_ID_SUFFIXES = (".json", ".png", ".tmx", ".tbin", ".xnb", ".ogg", ".wav", ".txt")
HAS_MOD = "hasmod"
TOKEN_OPEN, TOKEN_CLOSE = "{{", "}}"
SCANNED_SUFFIXES = frozenset({".json"})
CONTENT_FILE = "content.json"
NEGATED_VALUES = frozenset({"false"})
MAX_DETAIL = 160

log = logging.getLogger("layer-build")


@dataclass
class OtherMod:
    """One other mod the files mention, and what they do when it is (or is not) installed."""
    unique_id: str
    data_patches: list[str] = field(default_factory=list)   # patches to data the lexicon reads
    other_patches: int = 0                                  # patches to anything else
    loose_lines: list[str] = field(default_factory=list)    # HasMod lines outside a patch's When


def is_mod_id(text: str, own_ids: set[str]) -> bool:
    return text.lower() not in own_ids and not text.lower().endswith(NOT_MOD_ID_SUFFIXES)


def when_mods(when: object, own_ids: set[str]) -> list[tuple[str, bool]]:
    """(mod id, negated) for every HasMod condition in a patch's When block."""
    found: list[tuple[str, bool]] = []
    if not isinstance(when, dict):
        return found
    for key, value in when.items():
        if HAS_MOD not in str(key).lower():
            continue
        negated = str(value).strip().lower() in NEGATED_VALUES
        found += [(i, negated) for i in MOD_ID_PATTERN.findall(f"{key} {value}") if is_mod_id(i, own_ids)]
    return found


def read_json(path: Path) -> dict | None:
    try:
        data = json5.loads(path.read_text(encoding="utf-8-sig", errors="replace"))
    except ValueError as error:
        log.warning("Could not read %s (%s); its patches are not sorted.", path, error)
        return None
    return data if isinstance(data, dict) else None


def describe(patch: dict, source: Path, pack: Path, negated: bool) -> str:
    keys = list((patch.get("Entries") or patch.get("MapProperties") or patch.get("Fields") or {}).keys())
    parts = [
        "UNLESS installed:" if negated else "WITH it:",
        str(patch.get("Action", "?")),
        str(patch.get("Target", ""))[:70],
        f"({source.relative_to(pack).as_posix()}",
        f"{str(patch.get('LogName', ''))[:50]})",
    ]
    if patch.get("FromFile"):
        parts.append(f"from {str(patch['FromFile'])[-50:]}")
    if keys:
        parts.append(f"keys {str(keys)[:90]}")
    return " ".join(p for p in parts if p)[:MAX_DETAIL * 2]


def walk_patches(changes: object, source: Path, pack: Path, inherited: list[tuple[str, bool]],
                 own_ids: set[str], found: dict[str, OtherMod], seen: set[Path]) -> None:
    for patch in changes if isinstance(changes, list) else []:
        if not isinstance(patch, dict):
            continue
        mods = when_mods(patch.get("When"), own_ids) + inherited
        if patch.get("Action") == "Include":
            for name in str(patch.get("FromFile", "")).split(","):
                include = pack / name.strip()
                # A file included twice under different conditions is read under each; one already
                # being read (a loop) is not.
                if not include.is_file() or include in seen:
                    continue
                data = read_json(include)
                if data is not None:
                    walk_patches(data.get("Changes"), include, pack, mods, own_ids, found, seen | {include})
            continue
        targets = [t.strip().lower() for t in str(patch.get("Target", "")).split(",")]
        reads = any(t.startswith(READ_TARGETS) for t in targets)
        for mod_id, negated in mods:
            entry = found.setdefault(mod_id.lower(), OtherMod(mod_id))
            if reads:
                entry.data_patches.append(describe(patch, source, pack, negated))
            else:
                entry.other_patches += 1


def loose_lines(root: Path, own_ids: set[str], found: dict[str, OtherMod], sorted_ids: set[str]) -> None:
    """Every HasMod line in the folder naming a mod the patch walk did not sort."""
    for path in sorted(root.rglob("*")):
        if path.suffix.lower() not in SCANNED_SUFFIXES or not path.is_file():
            continue
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        for number, line in enumerate(lines, start=1):
            at = line.lower().find(HAS_MOD)
            if at < 0:
                continue
            tail = line[at:]
            # A {{HasMod ...}} token ends at its braces; what follows on the line (an action such
            # as spacechase0.SpaceCore_TriggerSpawnGroup) names no mod.
            if line[:at].rstrip().endswith(TOKEN_OPEN) and TOKEN_CLOSE in tail:
                tail = tail[:tail.index(TOKEN_CLOSE)]
            if tail.rstrip().endswith(":") and number < len(lines):
                tail += " " + lines[number]
            for other in MOD_ID_PATTERN.findall(tail):
                if not is_mod_id(other, own_ids) or other.lower() in sorted_ids:
                    continue
                place = f"{path.relative_to(root).as_posix()}:{number}: {line.strip()[:MAX_DETAIL]}"
                entry = found.setdefault(other.lower(), OtherMod(other))
                if place not in entry.loose_lines:
                    entry.loose_lines.append(place)


def mentions(root: Path, mod_ids: set[str]) -> bool:
    """Whether any file in the folder names one of these mod ids at all: a cheap test before the
    full reading of a large mod."""
    needles = [i.lower().encode("utf-8") for i in mod_ids]
    for path in root.rglob("*"):
        if path.suffix.lower() in SCANNED_SUFFIXES and path.is_file():
            text = path.read_bytes().lower()
            if any(needle in text for needle in needles):
                return True
    return False


def scan(roots: list[Path], own_ids: set[str]) -> list[OtherMod]:
    """Every other mod the folders' files mention. `own_ids` (lower case) holds the mod's own ids
    and those of everything it requires: content conditional on a required mod is always on."""
    found: dict[str, OtherMod] = {}
    for root in roots:
        for content in sorted(root.rglob(CONTENT_FILE)):
            data = read_json(content)
            if data is not None:
                walk_patches(data.get("Changes"), content, content.parent, [], own_ids, found, {content})
    sorted_ids = set(found)
    for root in roots:
        loose_lines(root, own_ids, found, sorted_ids)
    return sorted(found.values(), key=lambda m: m.unique_id.lower())
