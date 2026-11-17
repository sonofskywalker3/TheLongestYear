# /// script
# requires-python = ">=3.11"
# dependencies = ["json5"]
# ///
"""Hold a game open for a layer's hand checks: `tly_obtain`, `tly_itemmodel`, walks, sleeping to a date.

Usage:  uv run tools/layer-session.py <command> [arguments]

  on <UniqueID> [more ids]      enable exactly those mods and what they require; records what it renamed
  off                           put the Mods folder back as `on` found it
  launch                        start the game minimized and wait for the debug bridge
  close                         stop the game
  new                           make a farm (tly_newgame standard skipintro), play out any opening
                                scene a mod starts on it, print its save name
  load <save>                   load a save from the title screen
  ready                         play out a scene that is running, clear a new farm's focus picks and
                                choose the first weekly theme (any of them blocks tly_walkto and tly_sleep)
  scene                         play out the scene that is running now (after a walk into a new map)
  do <seconds> <regex> <cmd>... queue debug commands, wait for a log line matching the regex,
                                print every log line since
  tail <line> [regex]           print the log from a line number, optionally only matching lines
  count                         print the log's line count (a mark for `tail`)
  console <command>             send a SMAPI console command (the game's own `debug ...`)
  reload <save> <day> [season]  set the day (1 to 28) and season, sleep, reload the save, and print
                                how the lexicon was served: the hash-drift test
  delete <save>                 delete a farm this session made, with Farm Type Manager's files for it;
                                the farm that is loaded is deleted by the next `close` instead

Built on layer-build.py's own functions, so the game is driven the same way: through the debug
bridge, minimized, never by mouse or keyboard. `on` refuses while the game runs. Always finish with
`delete` for each farm made, then `close` and `off`.

A scene is stepped with tly_eventstep (it clicks the speech box on), a question is answered with its
first choice, and a stray menu is dismissed. That is enough for a mod's opening scene; a scene that
needs a real choice should be driven by hand with `do`.
"""
from __future__ import annotations

import importlib.util
import json
import logging
import re
import subprocess
import sys
import time
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
STATE_FILE = TOOLS.parent / "test-output" / "layer-build" / "session-renamed.json"
SERVED_PATTERN = re.compile(r"Lexicon: (Shipped|Cache|Computed)|Availability model [^:]*: (built|served)")
HUB_CARD_PATTERN = re.compile(r"Hub card slot \d+: (\w+)")
MAX_FOCUS_PICKS = 8
HUD_PATTERN = re.compile(r"eventUp=(True|False).*? menu=(\w+)")
HUD_EVENTS_PATTERN = re.compile(r"hud: .*events=\[([^\]]*)\]")
NO_EVENT_MARK = "tly_eventstep: no event"
LOADED_SAVE_PATTERN = re.compile(r"Context: loaded save '([^']+)'")
LEFT_SAVE_PATTERN = re.compile(r"tly_totitle: exiting|Context: returned to title")
QUESTION_MARK = "uestion"
# Menus that are the farm's own start, not a scene: `ready` deals with them.
OWN_MENUS = frozenset({"none", "FocusPickMenu", "WeeklyHubMenu"})
SCENE_POLLS = 150
SCENE_POLL_SECONDS = 2.5
QUIET_POLLS = 2
ORPHAN_FLAG_POLLS = 12
RENAMED_KEY = "renamed"
FARMS_KEY = "farms"
SETTLE_SECONDS = 4
SAVE_WAIT_SECONDS = 150
CONSOLE_SCRIPT = TOOLS / "send-smapi-command.ps1"

log = logging.getLogger("layer-session")


def load_builder():
    spec = importlib.util.spec_from_file_location("layer_build", TOOLS / "layer-build.py")
    module = importlib.util.module_from_spec(spec)
    sys.modules["layer_build"] = module
    spec.loader.exec_module(module)
    return module


lb = load_builder()


def read_state() -> dict[str, object]:
    """The session's record: the folders `on` renamed and the farms the next `close` deletes.
    An older file held the renamed folders alone."""
    state = json.loads(STATE_FILE.read_text(encoding="utf-8")) if STATE_FILE.exists() else {}
    if RENAMED_KEY not in state and FARMS_KEY not in state:
        state = {RENAMED_KEY: state}
    state.setdefault(RENAMED_KEY, {})
    state.setdefault(FARMS_KEY, [])
    return state


def write_state(state: dict[str, object]) -> None:
    if not state[RENAMED_KEY] and not state[FARMS_KEY]:
        STATE_FILE.unlink(missing_ok=True)
        return
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(json.dumps(state), encoding="utf-8")


def require_closed() -> None:
    if lb.game_running():
        raise lb.LayerBuildError("The game is running. Close it first (`close`), or leave it alone if it is not yours.")


def turn_on(unique_ids: list[str]) -> None:
    require_closed()
    report = lb.Report("session")
    needed = lb.resolve(unique_ids, lb.read_manifests(), report)
    state = read_state()
    try:
        lb.set_enabled(needed, state[RENAMED_KEY])
    finally:
        # Written even when a rename fails part way: `off` must know every folder that moved.
        write_state(state)
    log.info("Enabled: %s", ", ".join(sorted(needed)))


def turn_off() -> None:
    require_closed()
    state = read_state()
    renamed = state[RENAMED_KEY]
    lb.restore_folders(renamed)
    state[RENAMED_KEY] = {}
    write_state(state)
    log.info("Put back: %s", ", ".join(sorted(renamed.values())) or "nothing was renamed")


def loaded_save() -> str | None:
    """The save the running game has loaded, read from the log; None at the title screen."""
    for line in reversed(lb.log_lines()):
        if LEFT_SAVE_PATTERN.search(line):
            return None
        match = LOADED_SAVE_PATTERN.search(line)
        if match:
            return match.group(1)
    return None


def delete(save: str) -> None:
    """Deletes a farm now, unless the running game has it loaded: that one is recorded and goes at `close`."""
    if lb.game_running() and loaded_save() == save:
        state = read_state()
        if save not in state[FARMS_KEY]:
            state[FARMS_KEY].append(save)
        write_state(state)
        log.info("%s is the loaded farm: recorded, and `close` deletes it.", save)
        return
    lb.delete_farm(save)
    log.info("Deleted %s.", save)


def close() -> None:
    lb.close_game()
    log.info("Game closed.")
    state = read_state()
    for save in state[FARMS_KEY]:
        lb.delete_farm(save)
        log.info("Deleted %s.", save)
    state[FARMS_KEY] = []
    write_state(state)


def finish_scene() -> bool:
    """Plays out a scene that is running (a mod's opening scene on a new farm, an arrival scene after
    a walk): steps it until two polls in a row show no event and no menu of its own. False, with the
    reason logged, when it gives up."""
    quiet = orphan = 0
    stepped = False
    for _ in range(SCENE_POLLS):
        mark = lb.send("tly_hud")
        time.sleep(SCENE_POLL_SECONDS)
        lines = lb.log_lines()[mark:]
        hud = next((m for m in map(HUD_PATTERN.search, reversed(lines)) if m), None)
        if hud is None:
            continue
        event_up, menu = hud.group(1) == "True", hud.group(2)
        events = next((m.group(1) for m in map(HUD_EVENTS_PATTERN.search, reversed(lines)) if m), "")
        if not event_up and menu in OWN_MENUS:
            quiet += 1
            if quiet >= QUIET_POLLS:
                if stepped:
                    log.info("The scene is over.")
                return True
            continue
        quiet = 0
        if not stepped:
            log.info("A scene is playing (%s); stepping it.", events or menu)
            stepped = True
        if not event_up:
            lb.send("tly_dismiss")
            continue
        step_mark = lb.send("tly_eventstep")
        time.sleep(SCENE_POLL_SECONDS)
        replies = [line for line in lb.log_lines()[step_mark:] if "tly_eventstep:" in line]
        if any(NO_EVENT_MARK in line for line in replies):
            # The flag is up but the map the farmer stands on holds no event (the farmer was moved
            # out of a scene): nothing here can be stepped.
            orphan += 1
            lb.send("tly_dismiss")
            if orphan >= ORPHAN_FLAG_POLLS:
                log.error("Gave up: the game's event flag is up but this map holds no event (events elsewhere: %s). "
                          "Start a new farm.", events or "none")
                return False
            continue
        orphan = 0
        if any(QUESTION_MARK in line for line in replies):
            lb.send("tly_answer 0")
    log.error("Gave up: the scene was still playing after %d polls. Drive it by hand with `do`.", SCENE_POLLS)
    return False


def new_farm() -> None:
    before = {p.name for p in lb.SAVES_DIR.iterdir() if p.is_dir()}
    lb.run("tly_newgame standard skipintro layersession", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT)
    time.sleep(SETTLE_SECONDS)
    finish_scene()
    # The save folder appears with the first night; until then the farm exists only in memory.
    log.info("Farm made. New save folders so far: %s", ", ".join(lb.new_saves(before)) or "none until its first night")


def make_ready() -> None:
    if not finish_scene():
        return
    hub = re.compile(lb.HUB_OPEN_PATTERN)
    mark = max(0, len(lb.log_lines()) - 400)
    for _ in range(MAX_FOCUS_PICKS):
        if any(hub.search(line) for line in lb.log_lines()[mark:]):
            break
        lb.send("tly_focus card 0")
        time.sleep(SETTLE_SECONDS)
    cards_mark = lb.send("tly_hubcards")
    time.sleep(SETTLE_SECONDS)
    cards = [m.group(1) for m in map(HUB_CARD_PATTERN.search, lb.log_lines()[cards_mark:]) if m]
    if not cards:
        log.info("No weekly screen is open; nothing to choose.")
        return
    select_mark = lb.send(f"tly_select {cards[0]}")
    time.sleep(SETTLE_SECONDS)
    print_lines(lb.log_lines()[select_mark:], re.compile("tly_select"))


def print_lines(lines: list[str], only: re.Pattern[str] | None = None) -> None:
    for line in lines:
        if only is None or only.search(line):
            sys.stdout.write(line[:1500] + "\n")


def do(timeout: int, pattern: str, commands: list[str]) -> None:
    mark = lb.send(*commands)
    try:
        lb.wait_log(pattern, mark, timeout)
    except lb.LayerBuildError as error:
        log.error("%s", error)
    time.sleep(1)
    print_lines(lb.log_lines()[mark:])


def console(command: str) -> None:
    """A SMAPI console command (the game's own `debug ...`), which the bridge file does not carry."""
    subprocess.run(["pwsh", "-NoProfile", "-File", str(CONSOLE_SCRIPT), command], check=False, capture_output=True)
    time.sleep(SETTLE_SECONDS)


def reload_on(save: str, day: str, season: str | None) -> None:
    """Sleeps into the given date and loads the save again: a layer that ships on Spring 1 must
    still be served as shipped (same `Lexicon:` key, same availability inputs) on any other day."""
    save_file = lb.SAVES_DIR / save / save
    stamp = save_file.stat().st_mtime if save_file.exists() else 0.0
    lb.send(f"tly_setday {day}")
    time.sleep(SETTLE_SECONDS)
    if season:
        console(f"debug season {season}")
    lb.send("tly_sleep")
    deadline = time.monotonic() + SAVE_WAIT_SECONDS
    while time.monotonic() < deadline and not (save_file.exists() and save_file.stat().st_mtime > stamp):
        time.sleep(lb.POLL_SECONDS)
        lb.send("tly_dismiss")
    if not (save_file.exists() and save_file.stat().st_mtime > stamp):
        raise lb.LayerBuildError("The night did not save. Is a pick, the weekly screen or an event open? Run `ready` first.")
    time.sleep(SETTLE_SECONDS * 2)
    lb.send("tly_totitle")
    time.sleep(SETTLE_SECONDS * 2)
    mark = lb.send(f"tly_loadsave {save}")
    lb.wait_log(lb.RUN_READY_PATTERN, mark, lb.LOAD_TIMEOUT)
    print_lines(lb.log_lines()[mark:], SERVED_PATTERN)


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        sys.stdout.write(__doc__)
        return 0
    command, rest = args[0], args[1:]
    try:
        if command == "on" and rest:
            turn_on(rest)
        elif command == "off":
            turn_off()
        elif command == "launch":
            require_closed()
            lb.launch()
            log.info("Bridge ready.")
        elif command == "close":
            close()
        elif command == "scene":
            finish_scene()
        elif command == "new":
            new_farm()
        elif command == "load" and len(rest) == 1:
            log.info(lb.run(f"tly_loadsave {rest[0]}", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT))
        elif command == "ready":
            make_ready()
        elif command == "do" and len(rest) >= 3:
            do(int(rest[0]), rest[1], rest[2:])
        elif command == "tail" and rest:
            print_lines(lb.log_lines()[int(rest[0]):], re.compile(rest[1]) if len(rest) > 1 else None)
        elif command == "count":
            sys.stdout.write(f"{len(lb.log_lines())}\n")
        elif command == "console" and rest:
            console(" ".join(rest))
        elif command == "reload" and len(rest) in (2, 3):
            reload_on(rest[0], rest[1], rest[2] if len(rest) == 3 else None)
        elif command == "delete" and len(rest) == 1:
            delete(rest[0])
        else:
            sys.stdout.write(__doc__)
            return 2
    except lb.LayerBuildError as error:
        log.error("%s", error)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
