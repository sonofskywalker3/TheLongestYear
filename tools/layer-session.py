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
  new                           make a farm (tly_newgame standard skipintro), print its save name
  load <save>                   load a save from the title screen
  ready                         clear a new farm's focus picks and choose the first weekly theme
                                (an open pick or weekly screen blocks tly_walkto and tly_sleep)
  do <seconds> <regex> <cmd>... queue debug commands, wait for a log line matching the regex,
                                print every log line since
  tail <line> [regex]           print the log from a line number, optionally only matching lines
  count                         print the log's line count (a mark for `tail`)
  console <command>             send a SMAPI console command (the game's own `debug ...`)
  reload <save> <day> [season]  set the day (1 to 28) and season, sleep, reload the save, and print
                                how the lexicon was served: the hash-drift test
  delete <save>                 delete a farm this session made, with Farm Type Manager's files for it

Built on layer-build.py's own functions, so the game is driven the same way: through the debug
bridge, minimized, never by mouse or keyboard. `on` refuses while the game runs. Always finish with
`close`, `off`, and `delete` for each farm made.
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


def read_state() -> dict[str, str]:
    return json.loads(STATE_FILE.read_text(encoding="utf-8")) if STATE_FILE.exists() else {}


def require_closed() -> None:
    if lb.game_running():
        raise lb.LayerBuildError("The game is running. Close it first (`close`), or leave it alone if it is not yours.")


def turn_on(unique_ids: list[str]) -> None:
    require_closed()
    report = lb.Report("session")
    needed = lb.resolve(unique_ids, lb.read_manifests(), report)
    renamed = read_state()
    try:
        lb.set_enabled(needed, renamed)
    finally:
        # Written even when a rename fails part way: `off` must know every folder that moved.
        STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
        STATE_FILE.write_text(json.dumps(renamed), encoding="utf-8")
    log.info("Enabled: %s", ", ".join(sorted(needed)))


def turn_off() -> None:
    require_closed()
    renamed = read_state()
    lb.restore_folders(renamed)
    STATE_FILE.unlink(missing_ok=True)
    log.info("Put back: %s", ", ".join(sorted(renamed.values())) or "nothing was renamed")


def new_farm() -> None:
    before = {p.name for p in lb.SAVES_DIR.iterdir() if p.is_dir()}
    lb.run("tly_newgame standard skipintro layersession", lb.RUN_READY_PATTERN, lb.LOAD_TIMEOUT)
    # The save folder appears with the first night; until then the farm exists only in memory.
    log.info("Farm made. New save folders so far: %s", ", ".join(lb.new_saves(before)) or "none until its first night")


def make_ready() -> None:
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
            lb.close_game()
            log.info("Game closed.")
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
            require_closed()
            lb.delete_farm(rest[0])
            log.info("Deleted %s.", rest[0])
        else:
            sys.stdout.write(__doc__)
            return 2
    except lb.LayerBuildError as error:
        log.error("%s", error)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
