"""The combination pass of tools/layer-build.py: the answers for two supported mods installed together.

`uv run tools/layer-build.py <UniqueID A> <UniqueID B>` lands here. The pair is named `<A>+<B>` with
the ids in ordinal order, as the mod itself names it (docs/lexicon-layers.md, "Combinations"). Each
mod needs its own layer first. Hand-written routes for content that exists only with both installed
go in `<A>+<B>.inputs.json` before this runs; the file is optional.

It takes the builder module as an argument, because that file's name (layer-build.py) cannot be imported.
"""
from __future__ import annotations

import shutil
from types import ModuleType
from typing import Any

PAIR_JOINER = "+"
PAIR_SIZE = 2
INPUTS_SUFFIX = ".inputs.json"
IDS_SUFFIX = ".ids.json"
ANSWERS_SUFFIX = ".answers.json"
LAYER_SUFFIXES = (INPUTS_SUFFIX, IDS_SUFFIX, ANSWERS_SUFFIX)
PLAIN_PASS = "plain"


def pair_name(unique_ids: list[str]) -> str:
    """The combination's name: its mod ids sorted ordinally and joined, as LayerFile.Name does."""
    return PAIR_JOINER.join(sorted(unique_ids))


def passes_for_pair(lb: ModuleType, name: str, unique_ids: list[str]) -> list[Any]:
    """Every pass either mod, or the pair's own inputs, calls for: the plain one, one per door
    upgrade, and the Mixed Seeds pool pass. Each pass is listed once, in the order first met."""
    passes: list[Any] = []
    for layer in (*unique_ids, name):
        for layer_pass in lb.passes_for(layer):
            if all(layer_pass.name != known.name for known in passes):
                passes.append(layer_pass)
    return passes


def stage_files(lb: ModuleType, name: str, unique_ids: list[str], fresh_ids: bool) -> None:
    """Puts the repository's layer files for both mods and the pair into the deployed folder, so the
    game reads what is committed and the pair's answers are written onto the stored ones."""
    for layer in (*unique_ids, name):
        for suffix in LAYER_SUFFIXES:
            source = lb.REPO_LAYERS / f"{layer}{suffix}"
            if source.exists():
                shutil.copy2(source, lb.DEPLOYED_LAYERS / source.name)
    if fresh_ids:
        (lb.DEPLOYED_LAYERS / f"{name}{IDS_SUFFIX}").unlink(missing_ok=True)


def build_pair(lb: ModuleType, unique_ids: list[str], skip_pierre: bool, keep_saves: bool, fresh_ids: bool,
               only: str | None) -> Any:
    """Writes and checks `<A>+<B>.ids.json` and `<A>+<B>.answers.json`; returns the run's report."""
    if len(unique_ids) != PAIR_SIZE or len(set(unique_ids)) != PAIR_SIZE:
        raise lb.LayerBuildError(f"A combination is two different mods; got {', '.join(unique_ids)}.")
    name = pair_name(unique_ids)
    report = lb.Report(name)
    (lb.OUT_ROOT / name).mkdir(parents=True, exist_ok=True)
    if lb.game_running():
        raise lb.LayerBuildError("The game is running. Close it first; this script starts and stops it.")
    mods = lb.read_manifests()
    for unique_id in unique_ids:
        target = next((m for m in mods if m.unique_id.lower() == unique_id.lower()), None)
        if target is None:
            raise lb.LayerBuildError(f"No installed mod has the UniqueID {unique_id}.")
        for suffix in (INPUTS_SUFFIX, ANSWERS_SUFFIX):
            if not (lb.REPO_LAYERS / f"{unique_id}{suffix}").exists():
                raise lb.LayerBuildError(f"{unique_id} has no {unique_id}{suffix}: build its own layer first.")
        report.add(f"{unique_id} {target.version}.")
    has_pair_inputs = (lb.REPO_LAYERS / f"{name}{INPUTS_SUFFIX}").exists()
    report.add(f"Combination {name}: " + ("hand-written inputs found." if has_pair_inputs
                                           else "no inputs file (needed only when the pair has content of its own)."))
    stage_files(lb, name, unique_ids, fresh_ids)

    ids_before = lb.read_ids(lb.REPO_LAYERS / f"{name}{IDS_SUFFIX}")
    written: list[str] = []
    for layer_pass in passes_for_pair(lb, name, unique_ids):
        if only is not None and (layer_pass.name or PLAIN_PASS) != only:
            continue
        # The answers are written whether or not the pair has inputs of its own.
        lb.run_pass(name, layer_pass, mods, True, skip_pierre, keep_saves, report, written, mod_ids=unique_ids)
    for variant in lb.stored_variants(name):
        if variant not in written:
            report.ask(f"The answers file holds the variant `{variant}`, which this run did not write again.")
    lb.report_id_changes(ids_before, lb.read_ids(lb.REPO_LAYERS / f"{name}{IDS_SUFFIX}"), report)
    if written:
        lb.report_moves(name, report)
    return report
