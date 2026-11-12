# /// script
# requires-python = ">=3.11"
# dependencies = ["json5"]
# ///
"""Build or refresh a supported mod's lexicon layer with no hand steps.

Usage:  uv run tools/layer-build.py <UniqueID> [--skip-pierre] [--keep-saves] [--fresh-ids]

What it does, in order (docs/lexicon-layers.md, "Adding a supported mod"):
  1. Finds the mod and every mod it requires in the Mods folder (a leading dot marks a disabled
     folder), enables exactly those, and disables every other mod that is not on ALWAYS_ON.
  2. Session 1: a new farm, `tly_lexicon ids <UniqueID>`.
  3. Session 2: the same farm with the ids in force: `tly_lexicon gaps`, and when the layer has an
     inputs file, `tly_lexicon write <UniqueID>`.
  4. The Pierre year-2 variant of the same (unless --skip-pierre).
  5. Session 3: a restart, then `tly_lexicon status` and `tly_lexicon diff` as the check. When a
     fresh build still differs, steps 3 to 5 run again (up to MAX_ROUNDS).
  6. Copies the ids and answers files into src/TheLongestYear/assets/lexicon/layers/.
  7. Scans the mod's own files for content that depends on OTHER mods being installed.
  8. Puts the Mods folder back, deletes the farms it made, and writes
     test-output/layer-build/<UniqueID>/report.md: what was written, what could not be placed, and
     the questions a person has to answer.

It drives the game through the debug bridge only (docs/HEADLESS_DRIVING.md): no mouse, no keyboard,
the window starts minimized. It refuses to start while the game is running.
"""
from __future__ import annotations

import argparse
import logging
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

import json5

GAME_DIR = Path(os.environ.get("TLY_GAME_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"))
MODS_DIR = GAME_DIR / "Mods"
TLY_MOD_DIR = MODS_DIR / "TheLongestYear"
SMAPI_EXE = GAME_DIR / "StardewModdingAPI.exe"
BRIDGE_FILE = TLY_MOD_DIR / "tly_commands.txt"
DEPLOYED_LAYERS = TLY_MOD_DIR / "assets" / "lexicon" / "layers"
SAVES_DIR = Path(os.environ["APPDATA"]) / "StardewValley" / "Saves"
LOG_FILE = Path(os.environ["APPDATA"]) / "StardewValley" / "ErrorLogs" / "SMAPI-latest.txt"
REPO = Path(__file__).resolve().parent.parent
REPO_LAYERS = REPO / "src" / "TheLongestYear" / "assets" / "lexicon" / "layers"
OUT_ROOT = REPO / "test-output" / "layer-build"

ALWAYS_ON = frozenset({"ConsoleCommands", "ContentPatcher", "SaveBackup", "TheLongestYear", "GenericModConfigMenu"})
GAME_PROCESSES = ("StardewModdingAPI", "Stardew Valley")
DISABLED_PREFIX = "."
PIERRE_UPGRADE = "pierre_year2_seeds"
PIERRE_JP = 10000
SAVE_PREFIX = "standard_"
LAUNCH_TIMEOUT = 420
LOAD_TIMEOUT = 420
COMMAND_TIMEOUT = 180
SLEEP_TIMEOUT = 150
POLL_SECONDS = 1.5
SETTLE_SECONDS = 4
READY_PATTERN = r"Debug bridge: 'pause when window is inactive'"
RUN_READY_PATTERN = r"Run \d+ ready"
RESULT = r"The Longest Year\] "          # a command's own output, not the bridge's echo of it
HUB_OPEN_PATTERN = r"Opened planning hub"
MAX_FOCUS_PICKS = 6
MAX_ROUNDS = 3
HAS_MOD_PATTERNS = (
    re.compile(r'"HasMod(?:\s*\|contains=([\w.\-]+))?"\s*:\s*"?([^",}\n]*)', re.IGNORECASE),
    re.compile(r"HasMod\s*\|contains=([\w.\-]+)", re.IGNORECASE),
    re.compile(r"\{\{HasMod\s*:\s*([\w.\-]+)\}\}", re.IGNORECASE),
)
SCANNED_SUFFIXES = frozenset({".json"})

log = logging.getLogger("layer-build")


class LayerBuildError(RuntimeError):
    """A step that must succeed did not."""


@dataclass(frozen=True)
class ModFolder:
    top: str            # the top-level folder name under Mods, without the disabled prefix
    unique_id: str
    version: str
    requires: tuple[str, ...]
    path: Path


@dataclass
class Report:
    unique_id: str
    lines: list[str] = field(default_factory=list)
    questions: list[str] = field(default_factory=list)

    def add(self, text: str) -> None:
        log.info(text)
        self.lines.append(text)

    def ask(self, text: str) -> None:
        log.warning("QUESTION: %s", text)
        self.questions.append(text)


def top_folders() -> dict[str, Path]:
    """Every folder under Mods, keyed by its name without the disabled prefix."""
    return {p.name.removeprefix(DISABLED_PREFIX): p for p in MODS_DIR.iterdir() if p.is_dir()}


def read_manifests() -> list[ModFolder]:
    mods: list[ModFolder] = []
    for top, folder in top_folders().items():
        for manifest in folder.rglob("manifest.json"):
            try:
                data = json5.loads(manifest.read_text(encoding="utf-8-sig"))
            except ValueError as error:
                log.warning("Could not read %s (%s); skipped.", manifest, error)
                continue
            requires = [d.get("UniqueID", "") for d in data.get("Dependencies") or [] if d.get("IsRequired", True)]
            pack_for = (data.get("ContentPackFor") or {}).get("UniqueID")
            if pack_for:
                requires.append(pack_for)
            mods.append(ModFolder(top, str(data.get("UniqueID", "")), str(data.get("Version", "")),
                                  tuple(r for r in requires if r), manifest.parent))
    return mods


def resolve(unique_ids: list[str], mods: list[ModFolder], report: Report) -> set[str]:
    """The top-level folders needed for these mods and everything they require."""
    by_id = {m.unique_id.lower(): m for m in mods}
    needed: set[str] = set()
    queue = [u.lower() for u in unique_ids]
    seen: set[str] = set()
    while queue:
        uid = queue.pop()
        if uid in seen:
            continue
        seen.add(uid)
        mod = by_id.get(uid)
        if mod is None:
            if uid in {u.lower() for u in unique_ids}:
                raise LayerBuildError(f"No installed mod has the UniqueID {uid}.")
            report.ask(f"A required mod is not in the Mods folder: {uid}. Install it, or confirm it is not needed.")
            continue
        needed.add(mod.top)
        # Everything shipped in the same top-level folder loads with it, so its requirements count too.
        for sibling in (m for m in mods if m.top == mod.top):
            queue.extend(r.lower() for r in sibling.requires)
    return needed


def set_enabled(enabled: set[str]) -> dict[str, str]:
    """Enables exactly these folders plus ALWAYS_ON. Returns the names as they were, to restore."""
    before = {top: path.name for top, path in top_folders().items()}
    for top, path in top_folders().items():
        want = top if (top in enabled or top in ALWAYS_ON) else DISABLED_PREFIX + top
        if path.name != want:
            path.rename(path.with_name(want))
    return before


def restore_folders(before: dict[str, str]) -> None:
    for top, path in top_folders().items():
        was = before.get(top)
        if was and path.name != was:
            path.rename(path.with_name(was))


def game_running() -> bool:
    out = subprocess.run(["tasklist"], capture_output=True, text=True, check=False).stdout.lower()
    return any(name.lower() in out for name in GAME_PROCESSES)


def powershell(command: str) -> None:
    subprocess.run(["pwsh", "-NoProfile", "-Command", command], check=False, capture_output=True)


def launch() -> None:
    BRIDGE_FILE.unlink(missing_ok=True)
    powershell(f"Start-Process -FilePath '{SMAPI_EXE}' -WindowStyle Minimized")
    time.sleep(SETTLE_SECONDS)
    wait_log(READY_PATTERN, 0, LAUNCH_TIMEOUT)


def close_game() -> None:
    powershell("Get-Process -Name " + ",".join(f"'{n}'" for n in GAME_PROCESSES)
               + " -ErrorAction SilentlyContinue | Stop-Process -Force")
    deadline = time.monotonic() + COMMAND_TIMEOUT
    while game_running() and time.monotonic() < deadline:
        time.sleep(POLL_SECONDS)


def log_lines() -> list[str]:
    if not LOG_FILE.exists():
        return []
    return LOG_FILE.read_text(encoding="utf-8", errors="replace").splitlines()


def wait_log(pattern: str, from_line: int, timeout: int) -> str:
    """The first log line at or after from_line that matches; raises on timeout."""
    regex = re.compile(pattern)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        for line in log_lines()[from_line:]:
            if regex.search(line):
                return line
        time.sleep(POLL_SECONDS)
    raise LayerBuildError(f"Timed out after {timeout} s waiting for the log line /{pattern}/.")


def send(*commands: str) -> int:
    """Queues debug commands; returns the log length before they run, for wait_log."""
    mark = len(log_lines())
    existing = BRIDGE_FILE.read_text(encoding="utf-8").splitlines() if BRIDGE_FILE.exists() else []
    BRIDGE_FILE.write_text("\n".join([*existing, *commands]) + "\n", encoding="utf-8")
    return mark


def run(command: str, done_pattern: str, timeout: int = COMMAND_TIMEOUT) -> str:
    return wait_log(done_pattern, send(command), timeout)


def new_saves(before: set[str]) -> list[str]:
    return sorted(p.name for p in SAVES_DIR.iterdir() if p.is_dir() and p.name not in before
                  and p.name.startswith(SAVE_PREFIX))


def copy_back(name: str, report: Report) -> bool:
    source = DEPLOYED_LAYERS / name
    if not source.exists():
        return False
    REPO_LAYERS.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, REPO_LAYERS / name)
    report.add(f"Copied {name} into the repository ({source.stat().st_size:,} bytes).")
    return True


def write_variant(unique_id: str, has_inputs: bool, label: str, report: Report) -> None:
    """With a farm loaded and the mod's ids in force: gaps, and the answers when there is an inputs file."""
    gaps_name = f"lexicon-gaps-{label}.md"
    report.add(run(f"tly_lexicon gaps {gaps_name}", RESULT + r"tly_lexicon gaps").split("] ", 1)[-1])
    gaps = TLY_MOD_DIR / gaps_name
    if gaps.exists():
        shutil.copy2(gaps, OUT_ROOT / unique_id / gaps_name)
        gaps.unlink()
    if not has_inputs:
        return
    line = run(f"tly_lexicon write {unique_id}", RESULT + r"tly_lexicon write", LOAD_TIMEOUT)
    report.add(f"[{label}] " + line.split("] ", 1)[-1])
    if "WARN" in line or "refus" in line.lower():
        report.ask(f"`tly_lexicon write {unique_id}` did not write the {label} variant: {line.split('] ', 1)[-1]}")


def own_pierre_upgrade(save: str, report: Report) -> bool:
    """Buys Pierre's year-2 line, sleeps so it is saved, and reloads the farm. False when the night did not save."""
    save_file = SAVES_DIR / save / save
    stamp = save_file.stat().st_mtime if save_file.exists() else 0.0
    mark = send(f"tly_addjp {PIERRE_JP}", f"tly_buyupgrade {PIERRE_UPGRADE}")
    # A new farm opens its focus card picks one after another, then the weekly screen; a pick
    # still open stops the night. Each pick needs its own tick, so they are sent one at a time.
    hub = re.compile(HUB_OPEN_PATTERN)
    for _ in range(MAX_FOCUS_PICKS):
        if any(hub.search(line) for line in log_lines()[mark:]):
            break
        send("tly_focus card 0")
        time.sleep(SETTLE_SECONDS)
    send("tly_sleep")
    deadline = time.monotonic() + SLEEP_TIMEOUT
    while time.monotonic() < deadline:
        if save_file.exists() and save_file.stat().st_mtime > stamp:
            break
        time.sleep(POLL_SECONDS)
    else:
        report.ask(f"The farm did not save after `tly_sleep`, so the {PIERRE_UPGRADE} variant was not written. "
                   "Write it by hand (docs/lexicon-layers.md, step 5).")
        return False
    time.sleep(SETTLE_SECONDS)
    send("tly_totitle")
    time.sleep(SETTLE_SECONDS * 2)
    run(f"tly_loadsave {save}", RUN_READY_PATTERN, LOAD_TIMEOUT)
    return True


def scan_combinations(unique_id: str, mods: list[ModFolder], report: Report) -> None:
    """Lists everything in the mod's own files that is conditional on another mod being installed."""
    own_tops = {m.top for m in mods if m.unique_id.lower() == unique_id.lower()}
    own_ids = {m.unique_id.lower() for m in mods if m.top in own_tops}
    found: dict[str, list[str]] = {}
    for top in own_tops:
        root = top_folders()[top]
        for path in sorted(root.rglob("*")):
            if path.suffix.lower() not in SCANNED_SUFFIXES or not path.is_file():
                continue
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            for number, line in enumerate(text.splitlines(), start=1):
                for pattern in HAS_MOD_PATTERNS:
                    for match in pattern.finditer(line):
                        named = [g for g in match.groups() if g]
                        for other in re.split(r"[,\s]+", " ".join(named)):
                            other = other.strip().strip('"')
                            if "." not in other or other.lower() in own_ids or other.lower() in {"true", "false"}:
                                continue
                            place = f"{path.relative_to(root)}:{number}: {line.strip()[:160]}"
                            if place not in found.setdefault(other, []):
                                found[other].append(place)
    if not found:
        report.add("No content in the mod's files depends on another mod being installed.")
        return
    supported = {p.name.split(".inputs.json")[0].lower() for p in REPO_LAYERS.glob("*.inputs.json")}
    for other, places in sorted(found.items(), key=lambda kv: kv[0].lower()):
        kind = "a supported mod" if other.lower() in supported else "a mod with no layer"
        sample = "\n".join(f"      - `{p}`" for p in places[:6])
        more = f"\n      - ... and {len(places) - 6} more" if len(places) > 6 else ""
        report.ask(
            f"Combination content with **{other}** ({kind}): {len(places)} conditional line(s). "
            "What it ADDS is ignored unless you want it supported (a combination file). "
            "What it REMOVES or closes must go into the layer as a rule that applies when that mod is installed. "
            f"Support it, ignore it, or does it remove something?\n{sample}{more}")


def check_layer(unique_id: str, report: Report) -> bool:
    """A restart with the files in place must serve the layer, with nothing different. True when it does."""
    status = run("tly_lexicon status", RESULT + r"tly_lexicon status: origin")
    report.add(status.split("] ", 1)[-1])
    if "origin: shipped" not in status:
        report.ask("After a restart the answers are not served as shipped (see the status line above). "
                   "Check the layer's version, its variants and the log.")
    diff = run("tly_lexicon diff", RESULT + r"tly_lexicon diff", LOAD_TIMEOUT)
    report.add(diff.split("] ", 1)[-1])
    return "origin: shipped" in status and " 0 difference(s)" in diff


def stored_variants(unique_id: str) -> list[str]:
    """The save variants the repository's answers file holds."""
    path = REPO_LAYERS / f"{unique_id}.answers.json"
    if not path.exists():
        return []
    return sorted((json5.loads(path.read_text(encoding="utf-8")).get("Deltas") or {}).keys())


def save_log(out: Path, name: str) -> None:
    if LOG_FILE.exists():
        shutil.copy2(LOG_FILE, out / f"SMAPI-{name}.txt")


def build(unique_id: str, skip_pierre: bool, keep_saves: bool, fresh_ids: bool) -> Report:
    report = Report(unique_id)
    out = OUT_ROOT / unique_id
    out.mkdir(parents=True, exist_ok=True)
    if game_running():
        raise LayerBuildError("The game is running. Close it first; this script starts and stops it.")
    mods = read_manifests()
    needed = resolve([unique_id], mods, report)
    target = next(m for m in mods if m.unique_id.lower() == unique_id.lower())
    report.add(f"{unique_id} {target.version}; enabling {', '.join(sorted(needed))}.")
    has_inputs = (REPO_LAYERS / f"{unique_id}.inputs.json").exists()
    if has_inputs:
        shutil.copy2(REPO_LAYERS / f"{unique_id}.inputs.json", DEPLOYED_LAYERS / f"{unique_id}.inputs.json")
        checked = re.search(r'"CheckedVersion"\s*:\s*"([^"]*)"', (REPO_LAYERS / f"{unique_id}.inputs.json").read_text(encoding="utf-8"))
        if checked and checked.group(1) != target.version:
            report.ask(f"The inputs file was checked against {checked.group(1)} and {target.version} is installed. "
                       "Read the gaps report for what the update changed, then set CheckedVersion.")
    else:
        report.ask("There is no inputs file yet, so only the ids and the gaps report were written. "
                   "Read lexicon-gaps-base.md, write the inputs file, and run this again.")
    # The answers file is added to variant by variant, so the one in the repository is the base to
    # write onto. The ids file only grows too; --fresh-ids starts it over.
    if (REPO_LAYERS / f"{unique_id}.answers.json").exists():
        shutil.copy2(REPO_LAYERS / f"{unique_id}.answers.json", DEPLOYED_LAYERS / f"{unique_id}.answers.json")
    if fresh_ids:
        (DEPLOYED_LAYERS / f"{unique_id}.ids.json").unlink(missing_ok=True)
    elif (REPO_LAYERS / f"{unique_id}.ids.json").exists():
        shutil.copy2(REPO_LAYERS / f"{unique_id}.ids.json", DEPLOYED_LAYERS / f"{unique_id}.ids.json")
    written = ["base"]

    saves_before = {p.name for p in SAVES_DIR.iterdir() if p.is_dir()}
    folders_before = set_enabled(needed)
    try:
        launch()
        run("tly_newgame standard skipintro layerbuild", RUN_READY_PATTERN, LOAD_TIMEOUT)
        report.add(run(f"tly_lexicon ids {unique_id}", r"tly_lexicon ids:").split("] ", 1)[-1])
        made = new_saves(saves_before)
        if not made:
            raise LayerBuildError("The new farm's save folder was not found.")
        save = made[-1]
        save_log(out, "1-ids")
        close_game()
        # The farm as it was made, before any night: every round writes the base variant from it.
        fresh_farm = out / "farm"
        shutil.rmtree(fresh_farm, ignore_errors=True)
        shutil.copytree(SAVES_DIR / save, fresh_farm)

        # The answers a write stores read some facts (the catch rates) from the answers the load was
        # served, so a layer written over an out-of-date one can take a second round to agree with
        # a fresh build. Rounds stop at the first clean check.
        for round_number in range(1, MAX_ROUNDS + 1):
            shutil.rmtree(SAVES_DIR / save, ignore_errors=True)
            shutil.copytree(fresh_farm, SAVES_DIR / save)
            launch()
            run(f"tly_loadsave {save}", RUN_READY_PATTERN, LOAD_TIMEOUT)
            write_variant(unique_id, has_inputs, "base", report)
            if not skip_pierre and own_pierre_upgrade(save, report):
                report.add(run(f"tly_lexicon ids {unique_id}", r"tly_lexicon ids:").split("] ", 1)[-1])
                write_variant(unique_id, has_inputs, PIERRE_UPGRADE, report)
                if PIERRE_UPGRADE not in written:
                    written.append(PIERRE_UPGRADE)
            errors = sum(1 for line in log_lines() if " ERROR " in line)
            save_log(out, f"2-write-round{round_number}")
            if errors:
                report.ask(f"The SMAPI log holds {errors} ERROR line(s); it is saved beside this report as SMAPI-2-write-round{round_number}.txt.")
            close_game()

            copy_back(f"{unique_id}.ids.json", report)
            if not (has_inputs and copy_back(f"{unique_id}.answers.json", report)):
                break
            launch()
            run(f"tly_loadsave {save}", RUN_READY_PATTERN, LOAD_TIMEOUT)
            clean = check_layer(unique_id, report)
            save_log(out, f"3-check-round{round_number}")
            close_game()
            if clean:
                break
            if round_number == MAX_ROUNDS:
                report.ask(f"After {MAX_ROUNDS} rounds a fresh build still differs from the answers written "
                           "(see the last diff line above and lexicon-diff.md in the mod folder).")
            else:
                report.add(f"Round {round_number}: a fresh build still differs; writing again.")
        shutil.rmtree(fresh_farm, ignore_errors=True)
        for variant in stored_variants(unique_id):
            if has_inputs and variant not in written:
                report.ask(f"The answers file holds the variant `{variant}`, which this run did not write again. "
                           "It needs a farm that owns that upgrade or mod (docs/lexicon-layers.md).")
        scan_combinations(unique_id, mods, report)
    finally:
        close_game()
        restore_folders(folders_before)
        if not keep_saves:
            for name in new_saves(saves_before):
                shutil.rmtree(SAVES_DIR / name, ignore_errors=True)
    return report


def write_report(report: Report) -> Path:
    path = OUT_ROOT / report.unique_id / "report.md"
    body = [f"# Layer build: {report.unique_id}", "", "## What ran", ""]
    body += [f"- {line}" for line in report.lines]
    body += ["", f"## Questions ({len(report.questions)})", ""]
    body += [f"{n}. {q}" for n, q in enumerate(report.questions, start=1)] or ["None."]
    path.write_text("\n".join(body) + "\n", encoding="utf-8")
    return path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("unique_id", help="the mod's manifest UniqueID")
    parser.add_argument("--skip-pierre", action="store_true", help="write the base variant only")
    parser.add_argument("--keep-saves", action="store_true", help="leave the farm this run made")
    parser.add_argument("--fresh-ids", action="store_true", help="start the ids file over instead of adding to it")
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s", datefmt="%H:%M:%S")
    try:
        report = build(args.unique_id, args.skip_pierre, args.keep_saves, args.fresh_ids)
    except LayerBuildError as error:
        log.error("%s", error)
        return 1
    log.info("Report: %s (%d question(s)).", write_report(report), len(report.questions))
    return 0


if __name__ == "__main__":
    sys.exit(main())
