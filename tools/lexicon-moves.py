# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Which stored answers moved: the lexicon answer files in the working tree against a commit.

Usage:  uv run tools/lexicon-moves.py [--against <rev>] [--limit <n>] [name ...]

Run it after a rule change, once vanilla and every layer are regenerated. `tly_lexicon diff` ending
at 0 differences proves only that a file agrees with a fresh build under the SAME rules; this shows
what the rule change did to answers that were already shipped. Every line is either meant (say so
in the commit) or a regression.

With no names it reads vanilla.json and every layers/*.answers.json. A name is `vanilla` or a
layer's UniqueID. The full list goes to test-output/lexicon-moves.md; the summary is logged.

A layer file stores only what differs from the layers below it, so "gone" on a layer line can mean
the answer now equals the one below (read the same item in the file below), not that it vanished.
"""
from __future__ import annotations

import argparse
import json
import logging
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LEXICON = Path("src") / "TheLongestYear" / "assets" / "lexicon"
VANILLA = "vanilla"
OUT_FILE = REPO / "test-output" / "lexicon-moves.md"
DEFAULT_LIMIT = 60
EVERYWHERE = "every variant"
# Per item and difficulty: the fields a player would notice. Basis is prose that repeats them.
DERIVED_FIELDS = ("EarliestWeek", "HardWeek", "Effort", "Mode", "EarliestSeason", "GateSeason", "Source")
ROUTE_SETS = ("Obtain", "YearSort")
WEEK_MAPS = ("MapWeeks", "ShopWeeks", "CatchRates")
NAME_LISTS = ("UnreachedMaps", "Unresolved")
CONDITION_FLAGS = ("YearTwo", "GingerIsland", "OwnedOnly", "FewDays", "RainOnly", "Unresolved")

log = logging.getLogger("lexicon-moves")


def relative_path(name: str) -> Path:
    return LEXICON / "vanilla.json" if name == VANILLA else LEXICON / "layers" / f"{name}.answers.json"


def all_names() -> list[str]:
    layers = sorted(p.name.removesuffix(".answers.json") for p in (REPO / LEXICON / "layers").glob("*.answers.json"))
    return [VANILLA, *layers]


def committed(name: str, rev: str) -> dict | None:
    """The file as the commit holds it; None when the commit has no such file."""
    spec = f"{rev}:{relative_path(name).as_posix()}"
    result = subprocess.run(["git", "-C", str(REPO), "show", spec], capture_output=True, check=False)
    if result.returncode != 0:
        return None
    return json.loads(result.stdout.decode("utf-8-sig"))


def working(name: str) -> dict | None:
    path = REPO / relative_path(name)
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else None


def variants(data: dict) -> dict[str, dict]:
    return data.get("Variants") or data.get("Deltas") or {}


def route_text(route: dict, tables: list[str]) -> str:
    """One route as a line a person can compare: what it is, where it lands, what it needs."""
    conditions = route.get("Conditions") or {}
    lands = route.get("Lands")
    table = tables[lands] if isinstance(lands, int) and 0 <= lands < len(tables) else str(lands)
    flags = [flag for flag in CONDITION_FLAGS if conditions.get(flag)]
    needs = ", ".join(conditions.get("Requires") or [])
    return (f"{route.get('Kind')} {route.get('Reliability')} `{table}` {route.get('Detail')}"
            + (f" [needs {needs}]" if needs else "") + (f" [{', '.join(flags)}]" if flags else ""))


def flatten(variant: dict) -> dict[str, str]:
    """Every answer in one variant as `kind | key` -> a comparable text."""
    flat: dict[str, str] = {}
    tables = variant.get("Tables") or []
    for section in ROUTE_SETS:
        for item, routes in (variant.get(section) or {}).items():
            for text in sorted({route_text(r, tables) for r in routes or []}):
                flat[f"{section} route | {item} | {text}"] = "present"
    for section in WEEK_MAPS:
        for key, value in (variant.get(section) or {}).items():
            flat[f"{section} | {key}"] = json.dumps(value, sort_keys=True)
    for section in NAME_LISTS:
        for entry in variant.get(section) or []:
            flat[f"{section} | {entry}"] = "listed"
    for difficulty, model in (variant.get("Availability") or {}).items():
        for section in ("Derived", "EffortDerived"):
            for item, answer in (model.get(section) or {}).items():
                shown = {f: answer[f] for f in DERIVED_FIELDS if f in answer}
                flat[f"{section} | {item} | {difficulty}"] = json.dumps(shown, sort_keys=True)
        for section in ("DishBases", "YearTwoCropDishUnits"):
            for item, value in (model.get(section) or {}).items():
                flat[f"{section} | {item} | {difficulty}"] = json.dumps(value)
    return flat


def moves(old: dict, new: dict) -> dict[tuple[str, str, str], list[str]]:
    """(key, before, after) -> the variants it moved in."""
    found: dict[tuple[str, str, str], list[str]] = defaultdict(list)
    old_variants, new_variants = variants(old), variants(new)
    for name in sorted(set(old_variants) | set(new_variants)):
        before, after = flatten(old_variants.get(name) or {}), flatten(new_variants.get(name) or {})
        for key in sorted(set(before) | set(after)):
            if before.get(key) != after.get(key):
                found[(key, before.get(key, "absent"), after.get(key, "gone"))].append(name)
    return found


def merge_difficulties(found: dict[tuple[str, str, str], list[str]], variant_count: int) -> list[str]:
    """One line per move; the difficulty and kitchen keys that moved the same way are joined."""
    grouped: dict[tuple[str, str, str, str], list[str]] = defaultdict(list)
    for (key, before, after), where in found.items():
        head, _, difficulty = key.rpartition(" | ") if key.startswith(("Derived", "EffortDerived", "DishBases", "YearTwoCrop")) else (key, "", "")
        scope = EVERYWHERE if len(where) == variant_count else ", ".join(where)
        grouped[(head, before, after, scope)].append(difficulty)
    lines = []
    for (head, before, after, scope), difficulties in sorted(grouped.items()):
        tiers = sorted({d.split("|")[0] for d in difficulties if d})
        tier_text = f" ({', '.join(tiers)})" if tiers else ""
        lines.append(f"- `{head}`{tier_text}: {before} -> {after} [{scope}]")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("names", nargs="*", help="vanilla or a layer's UniqueID; default every file")
    parser.add_argument("--against", default="HEAD", help="the commit to compare with (default HEAD)")
    parser.add_argument("--limit", type=int, default=DEFAULT_LIMIT, help="lines logged per file (the report holds all)")
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(message)s")

    body = [f"# Lexicon answers moved against {args.against}", ""]
    total = 0
    for name in args.names or all_names():
        old, new = committed(name, args.against), working(name)
        if new is None:
            log.error("%s: no answers file in the working tree.", name)
            return 1
        if old is None:
            log.info("%s: new file (not in %s).", name, args.against)
            body += [f"## {name}", "", f"New file; {args.against} has none.", ""]
            continue
        lines = merge_difficulties(moves(old, new), max(len(variants(old)), len(variants(new))))
        total += len(lines)
        log.info("%s: %d answer(s) moved.", name, len(lines))
        for line in lines[:args.limit]:
            log.info("  %s", line)
        if len(lines) > args.limit:
            log.info("  ... and %d more in %s", len(lines) - args.limit, OUT_FILE)
        body += [f"## {name}", "", *(lines or ["Nothing moved."]), ""]
    OUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    OUT_FILE.write_text("\n".join(body), encoding="utf-8")
    log.info("%d answer(s) moved in all. Full list: %s", total, OUT_FILE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
