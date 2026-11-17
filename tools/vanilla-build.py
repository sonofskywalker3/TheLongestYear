# /// script
# requires-python = ">=3.11"
# dependencies = ["json5"]
# ///
"""Regenerate the vanilla lexicon files (vanilla.json, vanilla.ids.json) with no hand steps.

Usage:  uv run tools/vanilla-build.py [--fresh-ids] [--keep-saves]

The unmodded twin of tools/layer-build.py, whose launch, bridge and Mods-folder helpers it reuses
(docs/lexicon-layers.md, "Every release" and "Keep it current"):
  1. Disables every mod that is not on layer-build's ALWAYS_ON list.
  2. A new farm, `tly_lexicon ids vanilla`.
  3. A restart with the ids in force: `tly_lexicon write vanilla` on the base farm, then again once
     the farm owns Pierre's year 2 line.
  4. Copies both files into src/TheLongestYear/assets/lexicon/.
  5. A restart, then `tly_lexicon status` and `tly_lexicon diff` as the check.
  6. Puts the Mods folder back and deletes the farm it made.

Run it after bumping `Version` in manifest.json and deploying that build: the file is stamped with
the running version and served only to it.
"""
from __future__ import annotations

import argparse
import importlib.util
import logging
import shutil
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("layer_build", TOOLS / "layer-build.py")
lb = importlib.util.module_from_spec(_spec)
sys.modules["layer_build"] = lb
_spec.loader.exec_module(lb)

VANILLA = "vanilla"
FILES = ("vanilla.json", "vanilla.ids.json")
DEPLOYED = lb.TLY_MOD_DIR / "assets" / "lexicon"
REPO_LEXICON = lb.REPO_LAYERS.parent
OUT = lb.OUT_ROOT / VANILLA
IDS_DONE = r"tly_lexicon ids:"

log = logging.getLogger("vanilla-build")


def note(report: lb.Report, line: str) -> None:
    report.add(line.split("] ", 1)[-1])


def write(report: lb.Report, label: str) -> bool:
    line = lb.run(f"tly_lexicon write {VANILLA}", lb.RESULT + lb.WRITE_DONE_PATTERN, lb.LOAD_TIMEOUT)
    note(report, line)
    return f"wrote variant '{label}'" in line


def build(fresh_ids: bool, keep_saves: bool) -> tuple[lb.Report, bool]:
    report = lb.Report(VANILLA)
    OUT.mkdir(parents=True, exist_ok=True)
    if lb.game_running():
        raise lb.LayerBuildError("The game is running. Close it first; this script starts and stops it.")
    for name in FILES:
        shutil.copy2(REPO_LEXICON / name, DEPLOYED / name)
    if fresh_ids:
        (DEPLOYED / "vanilla.ids.json").unlink()
    ids_before = lb.read_ids(REPO_LEXICON / "vanilla.ids.json")
    saves_before = {p.name for p in lb.SAVES_DIR.iterdir() if p.is_dir()}
    renamed: dict[str, str] = {}
    save: str | None = None
    clean = False
    try:
        lb.set_enabled(set(), renamed)
        lb.launch()
        lb.run("tly_newgame standard skipintro layerbuild", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT)
        note(report, lb.run(f"tly_lexicon ids {VANILLA}", IDS_DONE))
        made = lb.new_saves(saves_before)
        if len(made) != 1:
            raise lb.LayerBuildError(f"Expected one new farm in the saves folder and found {len(made)}.")
        save = made[0]
        lb.save_log(OUT, "1-ids")
        lb.close_game()

        lb.launch()
        lb.run(f"tly_loadsave {save}", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT)
        wrote = write(report, "base")
        if wrote and lb.own_upgrade(save, lb.PIERRE_UPGRADE, report):
            note(report, lb.run(f"tly_lexicon ids {VANILLA}", IDS_DONE))
            wrote = write(report, lb.PIERRE_UPGRADE)
        else:
            wrote = False
        lb.save_log(OUT, "2-write")
        lb.close_game()
        if not wrote:
            report.ask("A variant was not written; nothing was copied into the repository.")
            return report, False
        for name in FILES:
            shutil.copy2(DEPLOYED / name, REPO_LEXICON / name)
            report.add(f"Copied {name} into the repository.")

        lb.launch()
        lb.run(f"tly_loadsave {save}", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT)
        clean = lb.check_layer(VANILLA, report)
        lb.save_log(OUT, "3-check")
    finally:
        try:
            lb.close_game()
        finally:
            lb.restore_folders(renamed)
            if save is not None and not keep_saves:
                lb.delete_farm(save)
    lb.report_id_changes(ids_before, lb.read_ids(REPO_LEXICON / "vanilla.ids.json"), report)
    return report, clean


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--fresh-ids", action="store_true", help="start the ids file over instead of adding to it")
    parser.add_argument("--keep-saves", action="store_true", help="leave the farm this run made")
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    try:
        report, clean = build(args.fresh_ids, args.keep_saves)
    except lb.LayerBuildError as error:
        log.error("%s", error)
        return 1
    path = lb.write_report(report)
    log.info("Report: %s (%d question(s), check %s)", path, len(report.questions), "clean" if clean else "NOT clean")
    return 0 if clean and not report.questions else 2


if __name__ == "__main__":
    sys.exit(main())
