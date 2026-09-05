"""Turn a SMAPI log holding N tly_genbundles runs into a full board listing + frequency report.
Usage: python tools/boards-report.py <log> <out.md> [title]"""
import re, collections, sys
log, out_path = sys.argv[1], sys.argv[2]; title = sys.argv[3] if len(sys.argv) > 3 else 'Generated boards'
L = open(log, encoding='utf-8', errors='replace').read().splitlines()
boards = []; cur = None; bundle = None
for line in L:
    m = re.search(r"tly_genbundles: generated for loop (\d+)", line)
    if m: cur = {'loop': int(m.group(1)), 'bundles': []}; boards.append(cur); continue
    if cur is None: continue
    if 'determinism' in line: cur = None; continue
    m = re.search(r"\]\s+\[(\d+)\] (.+?) \(pick (\d+) of (\d+)\)", line)
    if m: bundle = {'name': m.group(2), 'pick': int(m.group(3)), 'of': int(m.group(4)), 'slots': [], 'room': ''}; cur['bundles'].append(bundle); continue
    if bundle is None: continue
    m = re.search(r"\]\s+([\w' ]+?)/\d+ '.+?' \[\d+ slots", line)
    if m: bundle['room'] = m.group(1); continue
    m = re.search(r"\]\s+slots: (.+)$", line)
    if m:
        for s in m.group(1).split(', '):
            mm = re.match(r"(.+?) \((.+?)\)(?: x(\d+))?(?: q(\d))?$", s)
            if mm: bundle['slots'].append((mm.group(1), int(mm.group(3) or 1), int(mm.group(4) or 0)))
            else: bundle['slots'].append((s, 1, 0))
out = [f'# {title}\n', 'Read straight out of the game via tly_genbundles (headless bridge). q1 = silver ask, q2 = gold ask.\n']
itemboards = collections.defaultdict(set); itemcount = collections.Counter(); stacks = collections.defaultdict(list); names = collections.Counter(); quals = collections.Counter()
for b in boards:
    out.append(f"\n## Board {b['loop']}\n"); room = None
    for bu in b['bundles']:
        if bu['room'] != room: room = bu['room']; out.append(f"\n**{room}**\n")
        parts = []
        for name, st, q in bu['slots']:
            parts.append(name + (f" x{st}" if st > 1 else '') + (f" q{q}" if q else ''))
            itemboards[name].add(b['loop']); itemcount[name] += 1; stacks[name].append(st)
            if q: quals[name] += 1
        names[bu['name']] += 1
        out.append(f"- {bu['name']} (pick {bu['pick']} of {bu['of']}): " + ', '.join(parts))
out.append(f'\n\n# Frequent flyers: items on 5 or more of the {len(boards)} boards\n')
out.append('| Item | Boards | Slots | Stack range | Quality asks |\n|---|---|---|---|---|')
for name, bs in sorted(itemboards.items(), key=lambda kv: (-len(kv[1]), -itemcount[kv[0]])):
    if len(bs) >= 5: out.append(f"| {name} | {len(bs)} | {itemcount[name]} | {min(stacks[name])} to {max(stacks[name])} | {quals[name]} |")
out.append('\n# Variety\n')
out.append(f"- Distinct items: {len(itemboards)}; total slots: {sum(itemcount.values())}; items on exactly one board: {sum(1 for v in itemboards.values() if len(v) == 1)}")
out.append(f"- Distinct bundle names: {len(names)}; on every board: {', '.join(n for n, c in names.items() if c == len(boards))}")
out.append(f"- Slots with a quality ask: {sum(quals.values())} of {sum(itemcount.values())}")
out.append('\n# Biggest asks (top 30 by stack)\n')
allslots = [(st, name, b['loop'], bu['name'], q) for b in boards for bu in b['bundles'] for name, st, q in bu['slots']]
for st, name, loop, bn, q in sorted(allslots, reverse=True)[:30]: out.append(f"- {name} x{st}{' q' + str(q) if q else ''} in {bn} (board {loop})")
open(out_path, 'w', encoding='utf-8').write('\n'.join(out)); print(len(boards), 'boards', sum(itemcount.values()), 'slots')
